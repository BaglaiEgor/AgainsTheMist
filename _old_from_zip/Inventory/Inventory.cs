using UnityEngine;
using System;

public class Inventory : MonoBehaviour
{
    private const int DefaultHotbarSize = 9;

    [Header("Config")]
    [SerializeField] private int slotCount = 30;
    [SerializeField] private int hotbarSize = DefaultHotbarSize;

    [Header("Runtime")]
    [SerializeField] private int activeSlotIndex = 0;
    [SerializeField] private ItemData equippedItem;

    public event Action OnInventoryChanged;

    private InventoryItem[] items;

    void Awake()
    {
        slotCount = Mathf.Max(DefaultHotbarSize, slotCount);
        hotbarSize = Mathf.Clamp(Mathf.Max(hotbarSize, DefaultHotbarSize), 1, slotCount);
        activeSlotIndex = Mathf.Clamp(activeSlotIndex, 0, hotbarSize - 1);

        items = new InventoryItem[slotCount];
        for (int i = 0; i < items.Length; i++)
            items[i] = new InventoryItem();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        slotCount = Mathf.Max(DefaultHotbarSize, slotCount);
        hotbarSize = Mathf.Clamp(Mathf.Max(hotbarSize, DefaultHotbarSize), 1, slotCount);
        activeSlotIndex = Mathf.Clamp(activeSlotIndex, 0, hotbarSize - 1);
    }
#endif

    #region Access
    public int SlotCount => slotCount;
    public int HotbarSize => hotbarSize;

    public InventoryItem GetItem(int index)
    {
        if (items == null) return null;
        if (index < 0 || index >= items.Length) return null;
        return items[index];
    }

    public InventoryItem GetActiveItem()
    {
        return GetItem(activeSlotIndex);
    }

    public ItemData EquippedItem => equippedItem;
    #endregion

    #region Select
    public void SelectNextSlot()
    {
        activeSlotIndex = (activeSlotIndex + 1) % hotbarSize;
        OnInventoryChanged?.Invoke();
    }

    public void SelectPreviousSlot()
    {
        activeSlotIndex--;
        if (activeSlotIndex < 0)
            activeSlotIndex = hotbarSize - 1;

        OnInventoryChanged?.Invoke();
    }

    public int ActiveSlotIndex => activeSlotIndex;
    #endregion

    #region Add / Remove
    public void Add(ItemData item, int amount = 1)
    {
        foreach (var slot in items)
        {
            if (!slot.IsEmpty && slot.item == item)
            {
                int space = item.maxStack - slot.amount;
                int toAdd = Mathf.Min(space, amount);
                slot.amount += toAdd;
                amount -= toAdd;

                if (amount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }
        }

        foreach (var slot in items)
        {
            if (slot.IsEmpty)
            {
                int toAdd = Mathf.Min(amount, item.maxStack);
                slot.item = item;
                slot.amount = toAdd;
                amount -= toAdd;

                if (amount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }
        }

        if (amount > 0)
            Debug.Log("Inventory full, leftover: " + amount);

        OnInventoryChanged?.Invoke();
    }

    public void Remove(ItemData item, int amount)
    {
        foreach (var slot in items)
        {
            if (slot.item != item) continue;

            int take = Mathf.Min(slot.amount, amount);
            slot.amount -= take;
            amount -= take;

            if (slot.amount <= 0)
                slot.Clear();

            if (amount <= 0)
            {
                OnInventoryChanged?.Invoke();
                return;
            }
        }
    }
    #endregion

    #region Swap
    public void Swap(int a, int b)
    {
        if (a == b) return;
        if (a < 0 || b < 0 || a >= items.Length || b >= items.Length) return;

        (items[a], items[b]) = (items[b], items[a]);
        OnInventoryChanged?.Invoke();
        Debug.Log($"Inventory swapped: slot {a} <-> slot {b}");
    }

    #endregion

    #region Old code support

    // было нужно крафту
    public bool HasItem(ItemData item, int amount = 1)
    {
        if (item == null)
            return false;

        if (amount <= 0)
            return true;

        if (items == null)
            return false;

        int total = 0;
        foreach (var slot in items)
        {
            if (slot != null && !slot.IsEmpty && slot.item == item)
                total += slot.amount;
        }
        return total >= amount;
    }

    // было нужно игроку
    public ItemData GetCurrentItem()
    {
        var slot = GetActiveItem();
        return slot != null && !slot.IsEmpty ? slot.item : null;
    }

    // было нужно UI / контроллеру
    public void SetActiveSlot(int index)
    {
        activeSlotIndex = Mathf.Clamp(index, 0, hotbarSize - 1);
        OnInventoryChanged?.Invoke();
    }

    // было нужно для открытия UI
    public void ToggleUI()
    {

    }

    public bool CanAddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0 || items == null)
            return false;

        int remaining = amount;
        int maxStack = Mathf.Max(1, item.maxStack);

        foreach (var slot in items)
        {
            if (slot == null || slot.IsEmpty || slot.item != item)
                continue;

            int space = Mathf.Max(0, maxStack - slot.amount);
            if (space <= 0)
                continue;

            int toPlace = Mathf.Min(space, remaining);
            remaining -= toPlace;
            if (remaining <= 0)
                return true;
        }

        foreach (var slot in items)
        {
            if (slot == null || !slot.IsEmpty)
                continue;

            int toPlace = Mathf.Min(maxStack, remaining);
            remaining -= toPlace;
            if (remaining <= 0)
                return true;
        }

        return remaining <= 0;
    }

    public bool TryEquipFromSlot(int index)
    {
        if (items == null)
            return false;
        if (index < 0 || index >= items.Length)
            return false;

        InventoryItem slot = items[index];
        if (slot == null || slot.IsEmpty || slot.item == null)
            return false;

        ItemData itemToEquip = slot.item;
        if (itemToEquip.type != ItemType.Equipment)
        {
            Debug.Log("Only equipment items can be equipped");
            return false;
        }

        if (equippedItem == itemToEquip)
            return false;

        ItemData previousEquipped = equippedItem;
        ItemData originalSlotItem = slot.item;
        int originalSlotAmount = slot.amount;

        slot.amount -= 1;
        if (slot.amount <= 0)
            slot.Clear();

        if (previousEquipped != null && !CanAddItem(previousEquipped, 1))
        {
            slot.item = originalSlotItem;
            slot.amount = originalSlotAmount;
            return false;
        }

        equippedItem = itemToEquip;

        if (previousEquipped != null)
        {
            Add(previousEquipped, 1);
            return true;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TrySetSlot(int index, ItemData item, int amount)
    {
        if (items == null || index < 0 || index >= items.Length)
            return false;

        if (item == null || amount <= 0)
        {
            items[index].Clear();
            OnInventoryChanged?.Invoke();
            return true;
        }

        int maxStack = Mathf.Max(1, item.maxStack);
        if (amount > maxStack)
            return false;

        items[index].item = item;
        items[index].amount = amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TryRemoveFromSlot(int index, int amount, out ItemData removedItem, out int removedAmount)
    {
        removedItem = null;
        removedAmount = 0;

        if (items == null || index < 0 || index >= items.Length || amount <= 0)
            return false;

        InventoryItem slot = items[index];
        if (slot == null || slot.IsEmpty || slot.item == null || slot.amount <= 0)
            return false;

        removedItem = slot.item;
        removedAmount = Mathf.Min(amount, slot.amount);
        slot.amount -= removedAmount;
        if (slot.amount <= 0)
            slot.Clear();

        OnInventoryChanged?.Invoke();
        return removedAmount > 0;
    }

    public bool TryAddToSlot(int index, ItemData item, int amount, out int addedAmount)
    {
        addedAmount = 0;

        if (items == null || index < 0 || index >= items.Length || item == null || amount <= 0)
            return false;

        InventoryItem slot = items[index];
        if (slot == null)
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        if (slot.IsEmpty)
        {
            int toAdd = Mathf.Min(maxStack, amount);
            slot.item = item;
            slot.amount = toAdd;
            addedAmount = toAdd;
            OnInventoryChanged?.Invoke();
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
        OnInventoryChanged?.Invoke();
        return addedAmount > 0;
    }

    #endregion

}
