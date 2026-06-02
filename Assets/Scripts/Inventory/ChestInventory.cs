using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ChestInventory : MonoBehaviour, IItemContainer
{
    public static event Action<ChestInventory, bool> OnAnyChestOpenStateChanged;

    [Header("Config")]
    [SerializeField] private int slotCount = 12;
    [SerializeField] private LootTable lootTable;

    [Header("Interaction")]
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;
    [SerializeField] private bool startOpened;

    [Header("Runtime")]
    [SerializeField] private bool hasGeneratedLoot;
    [SerializeField] private bool initialized;
    [SerializeField] private bool isOpen;
    [SerializeField] private InventoryItem[] slots;

    private int transactionDepth;
    private bool hasPendingTransactionChange;

    public event Action OnChestChanged;
    public event Action<bool> OnChestOpenStateChanged;

    public int SlotCount => slotCount;
    public bool IsOpen => isOpen;
    public string ContainerName => name;
    public Transform ContainerTransform => transform;

    void Awake()
    {
        EnsureSlots();
        InitializeIfNeeded();
        GenerateLootIfNeeded();
        isOpen = startOpened;
    }

    public bool TryInteract(Transform interactor)
    {
        if (!CanInteract(interactor))
            return false;

        if (isOpen)
            Close();
        else
            Open();

        return true;
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return false;

        float maxDistance = Mathf.Max(0.2f, interactDistance);
        return Vector2.Distance(interactor.position, transform.position) <= maxDistance;
    }

    public void Open()
    {
        GenerateLootIfNeeded();

        if (isOpen)
            return;

        isOpen = true;
        OnChestOpenStateChanged?.Invoke(true);
        OnAnyChestOpenStateChanged?.Invoke(this, true);
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        OnChestOpenStateChanged?.Invoke(false);
        OnAnyChestOpenStateChanged?.Invoke(this, false);
    }

    public InventoryItem GetItem(int index)
    {
        EnsureSlots();
        if (index < 0 || index >= slots.Length)
            return null;

        return slots[index];
    }

    public bool Add(ItemData item, int amount = 1)
    {
        return InventoryTransactionService.TryInsertItem(this, item, amount);
    }

    private bool AddDirect(ItemData item, int amount = 1)
    {
        EnsureSlots();
        if (item == null || amount <= 0)
            return false;

        int remaining = amount;

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            if (slot.IsEmpty || slot.item != item)
                continue;

            int space = Mathf.Max(0, item.maxStack - slot.amount);
            if (space <= 0)
                continue;

            int toAdd = Mathf.Min(space, remaining);
            slot.amount += toAdd;
            remaining -= toAdd;

            if (remaining <= 0)
            {
                NotifyChanged();
                return true;
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            if (!slot.IsEmpty)
                continue;

            int toAdd = Mathf.Min(Mathf.Max(1, item.maxStack), remaining);
            slot.item = item;
            slot.amount = toAdd;
            remaining -= toAdd;

            if (remaining <= 0)
            {
                NotifyChanged();
                return true;
            }
        }

        if (remaining != amount)
            NotifyChanged();

        return remaining <= 0;
    }

    public bool Remove(ItemData item, int amount)
    {
        return InventoryTransactionService.TryConsume(this, item, amount);
    }

    private bool RemoveDirect(ItemData item, int amount)
    {
        EnsureSlots();
        if (item == null || amount <= 0)
            return false;

        int remaining = amount;
        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            if (slot.IsEmpty || slot.item != item)
                continue;

            int take = Mathf.Min(slot.amount, remaining);
            slot.amount -= take;
            remaining -= take;

            if (slot.amount <= 0)
                slot.Clear();

            if (remaining <= 0)
            {
                NotifyChanged();
                return true;
            }
        }

        if (remaining != amount)
            NotifyChanged();

        return remaining <= 0;
    }

    public bool TryExtractStack(int index, out ItemData item, out int amount)
    {
        item = null;
        amount = 0;

        EnsureSlots();
        if (index < 0 || index >= slots.Length)
            return false;

        InventoryItem slot = slots[index];
        if (slot == null || slot.IsEmpty || slot.item == null || slot.amount <= 0)
            return false;

        item = slot.item;
        amount = slot.amount;
        SetSlotFromTransaction(index, null, 0);
        return true;
    }

    public bool CanAddItem(ItemData item, int amount = 1)
    {
        EnsureSlots();
        if (item == null || amount <= 0 || slots == null)
            return false;

        int remaining = amount;
        int maxStack = Mathf.Max(1, item.maxStack);

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            if (slot == null || slot.IsEmpty || slot.item != item)
                continue;

            int space = Mathf.Max(0, maxStack - slot.amount);
            if (space <= 0)
                continue;

            remaining -= Mathf.Min(space, remaining);
            if (remaining <= 0)
                return true;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            remaining -= Mathf.Min(maxStack, remaining);
            if (remaining <= 0)
                return true;
        }

        return remaining <= 0;
    }

    public void Swap(int a, int b)
    {
        EnsureSlots();
        if (a == b) return;
        if (a < 0 || b < 0 || a >= slots.Length || b >= slots.Length) return;

        InventoryTransactionService.TrySwap(this, a, this, b);
    }

    public bool TrySetSlot(int index, ItemData item, int amount)
    {
        EnsureSlots();
        if (!IsValidSlot(index))
            return false;

        if (item != null && amount > Mathf.Max(1, item.maxStack))
            return false;

        SetSlotFromTransaction(index, item, amount);
        return true;
    }

    public bool TryRemoveFromSlot(int index, int amount, out ItemData removedItem, out int removedAmount)
    {
        removedItem = null;
        removedAmount = 0;

        EnsureSlots();
        if (slots == null || index < 0 || index >= slots.Length || amount <= 0)
            return false;

        InventoryItem slot = slots[index];
        if (slot == null || slot.IsEmpty || slot.item == null || slot.amount <= 0)
            return false;

        removedItem = slot.item;
        removedAmount = Mathf.Min(amount, slot.amount);
        SetSlotFromTransaction(index, slot.item, slot.amount - removedAmount);
        return removedAmount > 0;
    }

    public bool TryAddToSlot(int index, ItemData item, int amount, out int addedAmount)
    {
        addedAmount = 0;

        EnsureSlots();
        if (slots == null || index < 0 || index >= slots.Length || item == null || amount <= 0)
            return false;

        InventoryItem slot = slots[index];
        if (slot == null)
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        if (slot.IsEmpty)
        {
            int toAdd = Mathf.Min(maxStack, amount);
            SetSlotFromTransaction(index, item, toAdd);
            addedAmount = toAdd;
            return addedAmount > 0;
        }

        if (slot.item != item)
            return false;

        int space = Mathf.Max(0, maxStack - slot.amount);
        if (space <= 0)
            return false;

        int stacked = Mathf.Min(space, amount);
        SetSlotFromTransaction(index, item, slot.amount + stacked);
        addedAmount = stacked;
        return addedAmount > 0;
    }

    public bool IsValidSlot(int slotIndex)
    {
        EnsureSlots();
        return slots != null && slotIndex >= 0 && slotIndex < slots.Length;
    }

    public bool CanAccept(int slotIndex, ItemData item)
    {
        return IsValidSlot(slotIndex) && item != null;
    }

    public bool CanExtract(int slotIndex)
    {
        InventoryItem slot = GetItem(slotIndex);
        return slot != null && !slot.IsEmpty && slot.item != null && slot.amount > 0;
    }

    public void BeginTransaction()
    {
        transactionDepth++;
    }

    public void EndTransaction()
    {
        transactionDepth = Mathf.Max(0, transactionDepth - 1);
        if (transactionDepth > 0 || !hasPendingTransactionChange)
            return;

        hasPendingTransactionChange = false;
        OnChestChanged?.Invoke();
    }

    public void SetSlotFromTransaction(int slotIndex, ItemData item, int amount)
    {
        EnsureSlots();
        if (!IsValidSlot(slotIndex))
            return;

        InventoryItem slot = slots[slotIndex];
        if (item == null || amount <= 0)
        {
            slot.Clear();
            NotifyChanged();
            return;
        }

        slot.item = item;
        slot.amount = Mathf.Clamp(amount, 1, Mathf.Max(1, item.maxStack));
        NotifyChanged();
    }

    public List<SaveItemStack> CreateSlotSnapshot()
    {
        EnsureSlots();
        List<SaveItemStack> result = new List<SaveItemStack>();
        for (int i = 0; i < slots.Length; i++)
        {
            InventoryItem slot = slots[i];
            result.Add(new SaveItemStack
            {
                itemId = slot != null && !slot.IsEmpty ? SaveManager.GetItemId(slot.item) : string.Empty,
                amount = slot != null && !slot.IsEmpty ? slot.amount : 0
            });
        }

        return result;
    }

    public void RestoreSlotSnapshot(List<SaveItemStack> savedSlots, Func<string, ItemData> itemResolver)
    {
        EnsureSlots();
        hasGeneratedLoot = true;
        for (int i = 0; i < slots.Length; i++)
        {
            SaveItemStack savedSlot = savedSlots != null && i < savedSlots.Count ? savedSlots[i] : null;
            ItemData item = savedSlot != null && itemResolver != null ? itemResolver(savedSlot.itemId) : null;
            int amount = savedSlot != null ? savedSlot.amount : 0;
            SetSlotFromTransaction(i, item, amount);
        }
    }

    void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;
        EnsureSlots();
    }

    void GenerateLootIfNeeded()
    {
        if (hasGeneratedLoot || lootTable == null)
            return;

        LootTable.LootEntry[] entries = lootTable.Entries;
        hasGeneratedLoot = true;

        if (entries == null || entries.Length == 0)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            LootTable.LootEntry entry = entries[i];
            if (entry.item == null)
                continue;

            float chance = Mathf.Clamp01(entry.chance);
            if (chance <= 0f || UnityEngine.Random.value > chance)
                continue;

            int minAmount = Mathf.Max(1, entry.minAmount);
            int maxAmount = Mathf.Max(minAmount, entry.maxAmount);
            int amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);

            if (amount > 0)
                Add(entry.item, amount);
        }
    }

    void EnsureSlots()
    {
        slotCount = Mathf.Max(1, slotCount);

        if (slots != null && slots.Length == slotCount)
            return;

        InventoryItem[] previous = slots;
        slots = new InventoryItem[slotCount];

        for (int i = 0; i < slots.Length; i++)
            slots[i] = new InventoryItem();

        if (previous == null)
            return;

        int copyLength = Mathf.Min(previous.Length, slots.Length);
        for (int i = 0; i < copyLength; i++)
        {
            if (previous[i] == null || previous[i].IsEmpty)
                continue;

            slots[i].item = previous[i].item;
            slots[i].amount = previous[i].amount;
        }
    }

    private void NotifyChanged()
    {
        if (transactionDepth > 0)
        {
            hasPendingTransactionChange = true;
            return;
        }

        OnChestChanged?.Invoke();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        slotCount = Mathf.Max(1, slotCount);
        interactDistance = Mathf.Max(0.2f, interactDistance);
    }
#endif
}
