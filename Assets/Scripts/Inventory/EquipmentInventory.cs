using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum EquipmentSlotType
{
    Helmet = 0,
    Chest = 1,
    Boots = 2,
    Accessory1 = 3,
    Accessory2 = 4,
    ActiveSlot = 5
}

[DisallowMultipleComponent]
public class EquipmentInventory : MonoBehaviour, IItemContainer
{
    [Header("Runtime Slots")]
    [SerializeField] private ItemData helmetItem;
    [SerializeField] private ItemData chestItem;
    [SerializeField] private ItemData bootsItem;
    [SerializeField] private ItemData accessoryItem1;
    [SerializeField] private ItemData accessoryItem2;
    [FormerlySerializedAs("accessoryItem3")]
    [SerializeField] private ItemData activeSlotItem;

    public event Action OnEquipmentChanged;

    private readonly InventoryItem[] slotViews =
    {
        new InventoryItem(),
        new InventoryItem(),
        new InventoryItem(),
        new InventoryItem(),
        new InventoryItem(),
        new InventoryItem()
    };

    private int transactionDepth;
    private bool hasPendingTransactionChange;

    public string ContainerName => name;
    public int SlotCount => 6;
    public Transform ContainerTransform => transform;

    public InventoryItem GetItem(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return null;

        ItemData item = GetSlotItem(slotIndex);
        InventoryItem view = slotViews[slotIndex];
        view.item = item;
        view.amount = item != null ? 1 : 0;
        return view;
    }

    public bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    public bool CanAccept(int slotIndex, ItemData item)
    {
        if (!IsValidSlot(slotIndex) || item == null || item.type != ItemType.Equipment)
            return false;

        return MatchesSlot(ToEquipmentSlotType(slotIndex), item);
    }

    public bool CanExtract(int slotIndex)
    {
        return IsValidSlot(slotIndex) && GetSlotItem(slotIndex) != null;
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
        OnEquipmentChanged?.Invoke();
    }

    public void SetSlotFromTransaction(int slotIndex, ItemData item, int amount)
    {
        if (!IsValidSlot(slotIndex))
            return;

        if (item == null || amount <= 0)
        {
            SetSlotItem(slotIndex, null);
            NotifyChanged();
            return;
        }

        if (!CanAccept(slotIndex, item))
            return;

        SetSlotItem(slotIndex, item);
        NotifyChanged();
    }

    public ItemData GetEquippedItem(EquipmentSlotType slotType)
    {
        return GetSlotItem((int)slotType);
    }

    public bool TryFindTargetSlot(ItemData item, out int slotIndex)
    {
        slotIndex = -1;
        if (item == null || item.type != ItemType.Equipment)
            return false;

        if (item.activeEquipmentEffect != ActiveEquipmentEffectType.None)
        {
            slotIndex = (int)EquipmentSlotType.ActiveSlot;
            return GetSlotItem(slotIndex) == null || CanAccept(slotIndex, item);
        }

        if (item.equipmentType == EquipmentType.Accessory)
        {
            int[] accessorySlots =
            {
                (int)EquipmentSlotType.Accessory1,
                (int)EquipmentSlotType.Accessory2
            };

            for (int i = 0; i < accessorySlots.Length; i++)
            {
                if (GetSlotItem(accessorySlots[i]) == null)
                {
                    slotIndex = accessorySlots[i];
                    return true;
                }
            }

            return false;
        }

        slotIndex = item.equipmentType switch
        {
            EquipmentType.Helmet => (int)EquipmentSlotType.Helmet,
            EquipmentType.Chest => (int)EquipmentSlotType.Chest,
            EquipmentType.Boots => (int)EquipmentSlotType.Boots,
            _ => -1
        };

        return slotIndex >= 0;
    }

    public float GetCombinedFrostDamageMultiplier()
    {
        float result = 1f;
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = GetSlotItem(i);
            if (item == null || item.type != ItemType.Equipment)
                continue;

            result *= Mathf.Max(0f, item.frostDamageMultiplier);
        }

        return result;
    }

    public int GetTotalDefense()
    {
        int total = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = GetSlotItem(i);
            if (item == null || item.type != ItemType.Equipment)
                continue;

            total += Mathf.Max(0, item.defense);
        }

        return total;
    }

    public float GetCombinedPressureGrowthMultiplier()
    {
        float result = 1f;
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = GetSlotItem(i);
            if (item == null || item.type != ItemType.Equipment)
                continue;

            result *= Mathf.Max(0f, item.pressureGrowthMultiplier);
        }

        return result;
    }

    public static EquipmentSlotType ToEquipmentSlotType(int slotIndex)
    {
        return slotIndex switch
        {
            0 => EquipmentSlotType.Helmet,
            1 => EquipmentSlotType.Chest,
            2 => EquipmentSlotType.Boots,
            3 => EquipmentSlotType.Accessory1,
            4 => EquipmentSlotType.Accessory2,
            _ => EquipmentSlotType.ActiveSlot
        };
    }

    private static bool MatchesSlot(EquipmentSlotType slotType, ItemData item)
    {
        if (item == null)
            return false;

        return slotType switch
        {
            EquipmentSlotType.Helmet => item.equipmentType == EquipmentType.Helmet,
            EquipmentSlotType.Chest => item.equipmentType == EquipmentType.Chest,
            EquipmentSlotType.Boots => item.equipmentType == EquipmentType.Boots,
            EquipmentSlotType.Accessory1 => item.equipmentType == EquipmentType.Accessory && item.activeEquipmentEffect == ActiveEquipmentEffectType.None,
            EquipmentSlotType.Accessory2 => item.equipmentType == EquipmentType.Accessory && item.activeEquipmentEffect == ActiveEquipmentEffectType.None,
            EquipmentSlotType.ActiveSlot => item.activeEquipmentEffect == ActiveEquipmentEffectType.SnowClimb,
            _ => false
        };
    }

    private void NotifyChanged()
    {
        if (transactionDepth > 0)
        {
            hasPendingTransactionChange = true;
            return;
        }

        OnEquipmentChanged?.Invoke();
    }

    private ItemData GetSlotItem(int slotIndex)
    {
        return slotIndex switch
        {
            (int)EquipmentSlotType.Helmet => helmetItem,
            (int)EquipmentSlotType.Chest => chestItem,
            (int)EquipmentSlotType.Boots => bootsItem,
            (int)EquipmentSlotType.Accessory1 => accessoryItem1,
            (int)EquipmentSlotType.Accessory2 => accessoryItem2,
            (int)EquipmentSlotType.ActiveSlot => activeSlotItem,
            _ => null
        };
    }

    private void SetSlotItem(int slotIndex, ItemData item)
    {
        switch (slotIndex)
        {
            case (int)EquipmentSlotType.Helmet:
                helmetItem = item;
                break;
            case (int)EquipmentSlotType.Chest:
                chestItem = item;
                break;
            case (int)EquipmentSlotType.Boots:
                bootsItem = item;
                break;
            case (int)EquipmentSlotType.Accessory1:
                accessoryItem1 = item;
                break;
            case (int)EquipmentSlotType.Accessory2:
                accessoryItem2 = item;
                break;
            case (int)EquipmentSlotType.ActiveSlot:
                activeSlotItem = item;
                break;
        }
    }
}
