using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ChestInventory : MonoBehaviour
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

    public event Action OnChestChanged;
    public event Action<bool> OnChestOpenStateChanged;

    public int SlotCount => slotCount;
    public bool IsOpen => isOpen;

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
                OnChestChanged?.Invoke();
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
                OnChestChanged?.Invoke();
                return true;
            }
        }

        if (remaining != amount)
            OnChestChanged?.Invoke();

        return remaining <= 0;
    }

    public bool Remove(ItemData item, int amount)
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
                OnChestChanged?.Invoke();
                return true;
            }
        }

        if (remaining != amount)
            OnChestChanged?.Invoke();

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
        slot.Clear();
        OnChestChanged?.Invoke();
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

        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnChestChanged?.Invoke();
    }

    public bool TrySetSlot(int index, ItemData item, int amount)
    {
        EnsureSlots();
        if (slots == null || index < 0 || index >= slots.Length)
            return false;

        if (item == null || amount <= 0)
        {
            slots[index].Clear();
            OnChestChanged?.Invoke();
            return true;
        }

        int maxStack = Mathf.Max(1, item.maxStack);
        if (amount > maxStack)
            return false;

        slots[index].item = item;
        slots[index].amount = amount;
        OnChestChanged?.Invoke();
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
        slot.amount -= removedAmount;
        if (slot.amount <= 0)
            slot.Clear();

        OnChestChanged?.Invoke();
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
            slot.item = item;
            slot.amount = toAdd;
            addedAmount = toAdd;
            OnChestChanged?.Invoke();
            return addedAmount > 0;
        }

        if (slot.item != item)
            return false;

        int space = Mathf.Max(0, maxStack - slot.amount);
        if (space <= 0)
            return false;

        int stacked = Mathf.Min(space, amount);
        slot.amount += stacked;
        addedAmount = stacked;
        OnChestChanged?.Invoke();
        return addedAmount > 0;
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

#if UNITY_EDITOR
    void OnValidate()
    {
        slotCount = Mathf.Max(1, slotCount);
        interactDistance = Mathf.Max(0.2f, interactDistance);
    }
#endif
}
