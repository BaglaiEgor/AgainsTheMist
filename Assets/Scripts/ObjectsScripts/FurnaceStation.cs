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
public class FurnaceStation : MonoBehaviour, IItemContainer
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
    private int transactionDepth;
    private bool hasPendingTransactionChange;

    public event Action OnFurnaceChanged;
    public event Action<bool> OnFurnaceOpenStateChanged;

    public bool IsOpen => isOpen;
    public string ContainerName => name;
    public int SlotCount => 3;
    public Transform ContainerTransform => transform;
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

    public InventoryItem GetItem(int slotIndex)
    {
        return GetSlot(ToSlotType(slotIndex));
    }

    public static int ToSlotIndex(FurnaceSlotType slotType)
    {
        return slotType switch
        {
            FurnaceSlotType.Fuel => 0,
            FurnaceSlotType.Input => 1,
            _ => 2,
        };
    }

    public static FurnaceSlotType ToSlotType(int slotIndex)
    {
        return slotIndex switch
        {
            0 => FurnaceSlotType.Fuel,
            1 => FurnaceSlotType.Input,
            _ => FurnaceSlotType.Output,
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

    public bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    public bool CanAccept(int slotIndex, ItemData item)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        return CanAcceptItem(ToSlotType(slotIndex), item);
    }

    public bool CanExtract(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return false;

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
        OnFurnaceChanged?.Invoke();
    }

    public void SetSlotFromTransaction(int slotIndex, ItemData item, int amount)
    {
        if (!IsValidSlot(slotIndex))
            return;

        FurnaceSlotType slotType = ToSlotType(slotIndex);
        InventoryItem slot = GetSlot(slotType);
        if (slot == null)
            return;

        if (item == null || amount <= 0)
        {
            slot.Clear();
            NotifyChanged();
            return;
        }

        if (!CanAcceptItem(slotType, item) && slotType != FurnaceSlotType.Output)
            return;

        slot.item = item;
        slot.amount = Mathf.Clamp(amount, 1, Mathf.Max(1, item.maxStack));
        NotifyChanged();
    }

    public SaveFurnaceData CreateSnapshot()
    {
        SaveFurnaceData data = new SaveFurnaceData
        {
            fuelTimeRemaining = fuelTimeRemaining,
            fuelTimeTotal = fuelTimeTotal,
            smeltProgress = smeltProgress
        };

        for (int i = 0; i < SlotCount; i++)
        {
            InventoryItem slot = GetItem(i);
            data.slots.Add(new SaveItemStack
            {
                itemId = slot != null && !slot.IsEmpty ? SaveManager.GetItemId(slot.item) : string.Empty,
                amount = slot != null && !slot.IsEmpty ? slot.amount : 0
            });
        }

        return data;
    }

    public void RestoreSnapshot(SaveFurnaceData data, Func<string, ItemData> itemResolver)
    {
        EnsureSlots();
        if (data == null)
            return;

        fuelTimeRemaining = Mathf.Max(0f, data.fuelTimeRemaining);
        fuelTimeTotal = Mathf.Max(0f, data.fuelTimeTotal);
        smeltProgress = Mathf.Max(0f, data.smeltProgress);

        for (int i = 0; i < SlotCount; i++)
        {
            SaveItemStack stack = data.slots != null && i < data.slots.Count ? data.slots[i] : null;
            ItemData item = stack != null && itemResolver != null ? itemResolver(stack.itemId) : null;
            int amount = stack != null ? stack.amount : 0;
            SetSlotFromTransaction(i, item, amount);
        }
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
            SetSlotFromTransaction(ToSlotIndex(slotType), item, toAdd);
            addedAmount = toAdd;
            return addedAmount > 0;
        }

        if (slot.item != item)
            return false;

        int space = Mathf.Max(0, maxStack - slot.amount);
        if (space <= 0)
            return false;

        int stacked = Mathf.Min(space, amount);
        SetSlotFromTransaction(ToSlotIndex(slotType), item, slot.amount + stacked);
        addedAmount = stacked;
        return addedAmount > 0;
    }

    public bool TrySetSlot(FurnaceSlotType slotType, ItemData item, int amount)
    {
        InventoryItem slot = GetSlot(slotType);
        if (slot == null)
            return false;

        if (item == null || amount <= 0)
        {
            SetSlotFromTransaction(ToSlotIndex(slotType), null, 0);
            return true;
        }

        if (!CanAcceptItem(slotType, item))
            return false;

        int maxStack = Mathf.Max(1, item.maxStack);
        if (amount > maxStack)
            return false;

        SetSlotFromTransaction(ToSlotIndex(slotType), item, amount);
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

        SetSlotFromTransaction(ToSlotIndex(slotType), slot.item, slot.amount - removedAmount);
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

        BeginTransaction();
        try
        {
            SetSlotFromTransaction(ToSlotIndex(FurnaceSlotType.Fuel), fuelSlot.item, fuelSlot.amount - 1);
            fuelTimeRemaining = burnSeconds;
            fuelTimeTotal = burnSeconds;
            NotifyChanged();
        }
        finally
        {
            EndTransaction();
        }

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

        BeginTransaction();
        try
        {
            SetSlotFromTransaction(ToSlotIndex(FurnaceSlotType.Input), inputSlot.item, inputSlot.amount - 1);
            int nextOutputAmount = outputSlot.IsEmpty ? producedAmount : outputSlot.amount + producedAmount;
            SetSlotFromTransaction(ToSlotIndex(FurnaceSlotType.Output), resultItem, nextOutputAmount);
        }
        finally
        {
            EndTransaction();
        }

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
        if (transactionDepth > 0)
        {
            hasPendingTransactionChange = true;
            return;
        }

        OnFurnaceChanged?.Invoke();
    }
}
