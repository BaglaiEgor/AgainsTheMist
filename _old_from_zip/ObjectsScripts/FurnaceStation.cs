using System;
using UnityEngine;

public enum FurnaceSlotType
{
    Fuel = 0,
    Input = 1,
    Output = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CraftingStation))]
public class FurnaceStation : MonoBehaviour
{
    public static event Action<FurnaceStation, bool> OnAnyFurnaceOpenStateChanged;

    [Header("Smelting")]
    [Min(0.1f)] [SerializeField] private float defaultSmeltDuration = 2f;

    [Header("Runtime Slots")]
    [SerializeField] private InventoryItem fuelSlot = new InventoryItem();
    [SerializeField] private InventoryItem inputSlot = new InventoryItem();
    [SerializeField] private InventoryItem outputSlot = new InventoryItem();

    [Header("Runtime State")]
    [SerializeField] private bool isOpen;
    [SerializeField] private float fuelTimeRemaining;
    [SerializeField] private float fuelTimeTotal;
    [SerializeField] private float smeltProgress;

    public event Action OnFurnaceChanged;
    public event Action<bool> OnFurnaceOpenStateChanged;

    public bool IsOpen => isOpen;
    public float FuelTimeRemaining => fuelTimeRemaining;
    public float FuelTimeTotal => fuelTimeTotal;
    public float SmeltProgress => smeltProgress;
    public float CurrentSmeltDuration => GetCurrentSmeltDuration();

    private void Awake()
    {
        EnsureSlots();
    }

    private void Update()
    {
        EnsureSlots();
        TickSmelting(Time.deltaTime);
    }

    public void Open()
    {
        if (isOpen)
            return;

        isOpen = true;
        OnFurnaceOpenStateChanged?.Invoke(true);
        OnAnyFurnaceOpenStateChanged?.Invoke(this, true);
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        OnFurnaceOpenStateChanged?.Invoke(false);
        OnAnyFurnaceOpenStateChanged?.Invoke(this, false);
    }

    public InventoryItem GetSlot(FurnaceSlotType slotType)
    {
        EnsureSlots();
        return slotType switch
        {
            FurnaceSlotType.Fuel => fuelSlot,
            FurnaceSlotType.Input => inputSlot,
            _ => outputSlot,
        };
    }

    public bool CanAcceptItem(FurnaceSlotType slotType, ItemData item)
    {
        if (item == null)
            return false;

        if (slotType == FurnaceSlotType.Fuel)
            return item.furnaceFuelSeconds > 0f;

        if (slotType == FurnaceSlotType.Input)
            return item.furnaceSmeltResult != null;

        return false;
    }

    public bool CanAddToSlot(FurnaceSlotType slotType, ItemData item, int amount)
    {
        if (amount <= 0 || item == null)
            return false;

        if (!CanAcceptItem(slotType, item))
            return false;

        InventoryItem slot = GetSlot(slotType);
        if (slot == null)
            return false;

        if (slot.IsEmpty)
            return amount <= Mathf.Max(1, item.maxStack);

        if (slot.item != item)
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        return slot.amount + amount <= maxStack;
    }

    public bool TryAddToSlot(FurnaceSlotType slotType, ItemData item, int amount, out int addedAmount)
    {
        addedAmount = 0;

        if (amount <= 0 || item == null)
            return false;

        if (!CanAcceptItem(slotType, item))
            return false;

        InventoryItem slot = GetSlot(slotType);
        if (slot == null)
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        if (slot.IsEmpty)
        {
            int toAdd = Mathf.Min(maxStack, amount);
            slot.item = item;
            slot.amount = toAdd;
            addedAmount = toAdd;
            NotifyChanged();
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
        NotifyChanged();
        return addedAmount > 0;
    }

    public bool TrySetSlot(FurnaceSlotType slotType, ItemData item, int amount)
    {
        InventoryItem slot = GetSlot(slotType);
        if (slot == null)
            return false;

        if (item == null || amount <= 0)
        {
            slot.Clear();
            NotifyChanged();
            return true;
        }

        if (!CanAcceptItem(slotType, item))
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        if (amount > maxStack)
            return false;

        slot.item = item;
        slot.amount = amount;
        NotifyChanged();
        return true;
    }

    public bool TryRemoveFromSlot(FurnaceSlotType slotType, int amount, out ItemData removedItem, out int removedAmount)
    {
        removedItem = null;
        removedAmount = 0;

        InventoryItem slot = GetSlot(slotType);
        if (slot == null || slot.IsEmpty || slot.item == null || amount <= 0)
            return false;

        removedItem = slot.item;
        removedAmount = Mathf.Min(amount, slot.amount);

        slot.amount -= removedAmount;
        if (slot.amount <= 0)
            slot.Clear();

        NotifyChanged();
        return removedAmount > 0;
    }

    private void TickSmelting(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        bool hasWork = CanSmeltCurrentInput();
        if (!hasWork)
        {
            if (smeltProgress > 0f)
            {
                smeltProgress = 0f;
                NotifyChanged();
            }

            return;
        }

        bool changed = false;

        if (fuelTimeRemaining > 0f)
        {
            fuelTimeRemaining = Mathf.Max(0f, fuelTimeRemaining - deltaTime);
            changed = true;
        }

        if (fuelTimeRemaining <= 0f && TryConsumeFuel())
            changed = true;

        if (fuelTimeRemaining <= 0f)
        {
            if (changed)
                NotifyChanged();
            return;
        }

        float smeltDuration = GetCurrentSmeltDuration();
        smeltProgress += deltaTime;
        changed = true;

        while (smeltProgress >= smeltDuration)
        {
            if (!CanSmeltCurrentInput())
            {
                smeltProgress = 0f;
                break;
            }

            if (!CompleteOneSmelt())
            {
                smeltProgress = Mathf.Min(smeltProgress, smeltDuration);
                break;
            }

            smeltProgress -= smeltDuration;
            smeltDuration = GetCurrentSmeltDuration();
        }

        if (changed)
            NotifyChanged();
    }

    private bool TryConsumeFuel()
    {
        if (fuelSlot == null || fuelSlot.IsEmpty || fuelSlot.item == null || fuelSlot.amount <= 0)
            return false;

        float burnSeconds = Mathf.Max(0f, fuelSlot.item.furnaceFuelSeconds);
        if (burnSeconds <= 0f)
            return false;

        fuelSlot.amount -= 1;
        if (fuelSlot.amount <= 0)
            fuelSlot.Clear();

        fuelTimeRemaining = burnSeconds;
        fuelTimeTotal = burnSeconds;
        return true;
    }

    private bool CanSmeltCurrentInput()
    {
        if (inputSlot == null || inputSlot.IsEmpty || inputSlot.item == null || inputSlot.amount <= 0)
            return false;

        ItemData sourceItem = inputSlot.item;
        ItemData resultItem = sourceItem.furnaceSmeltResult;
        if (resultItem == null)
            return false;

        int producedAmount = Mathf.Max(1, sourceItem.furnaceSmeltResultAmount);
        int maxStack = Mathf.Max(1, resultItem.maxStack);

        if (outputSlot == null)
            return false;

        if (outputSlot.IsEmpty)
            return producedAmount <= maxStack;

        if (outputSlot.item != resultItem)
            return false;

        return outputSlot.amount + producedAmount <= maxStack;
    }

    private bool CompleteOneSmelt()
    {
        if (!CanSmeltCurrentInput())
            return false;

        ItemData sourceItem = inputSlot.item;
        ItemData resultItem = sourceItem.furnaceSmeltResult;
        int producedAmount = Mathf.Max(1, sourceItem.furnaceSmeltResultAmount);

        inputSlot.amount -= 1;
        if (inputSlot.amount <= 0)
            inputSlot.Clear();

        if (outputSlot.IsEmpty)
        {
            outputSlot.item = resultItem;
            outputSlot.amount = producedAmount;
            return true;
        }

        outputSlot.amount += producedAmount;
        return true;
    }

    private float GetCurrentSmeltDuration()
    {
        ItemData sourceItem = inputSlot != null ? inputSlot.item : null;
        float sourceDuration = sourceItem != null ? sourceItem.furnaceSmeltDuration : 0f;
        return Mathf.Max(0.1f, sourceDuration > 0f ? sourceDuration : defaultSmeltDuration);
    }

    private void EnsureSlots()
    {
        if (fuelSlot == null)
            fuelSlot = new InventoryItem();

        if (inputSlot == null)
            inputSlot = new InventoryItem();

        if (outputSlot == null)
            outputSlot = new InventoryItem();
    }

    private void NotifyChanged()
    {
        OnFurnaceChanged?.Invoke();
    }
}
