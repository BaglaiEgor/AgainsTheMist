using UnityEngine;

public interface IItemContainer
{
    string ContainerName { get; }
    int SlotCount { get; }
    Transform ContainerTransform { get; }

    InventoryItem GetItem(int slotIndex);
    bool IsValidSlot(int slotIndex);
    bool CanAccept(int slotIndex, ItemData item);
    bool CanExtract(int slotIndex);

    void BeginTransaction();
    void EndTransaction();
    void SetSlotFromTransaction(int slotIndex, ItemData item, int amount);
}

public readonly struct ItemSlotRef
{
    public readonly IItemContainer container;
    public readonly int slotIndex;

    public ItemSlotRef(IItemContainer container, int slotIndex)
    {
        this.container = container;
        this.slotIndex = slotIndex;
    }

    public bool IsValid => container != null && container.IsValidSlot(slotIndex);
}

public readonly struct ItemStackSnapshot
{
    public readonly ItemData item;
    public readonly int amount;

    public ItemStackSnapshot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = Mathf.Max(0, amount);
    }

    public bool IsEmpty => item == null || amount <= 0;

    public static ItemStackSnapshot FromSlot(InventoryItem slot)
    {
        if (slot == null || slot.IsEmpty)
            return new ItemStackSnapshot(null, 0);

        return new ItemStackSnapshot(slot.item, slot.amount);
    }
}
