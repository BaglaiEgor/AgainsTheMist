using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EquipmentSlotUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [SerializeField] private bool tintBackgroundByState;
    [SerializeField] private Color emptyBackgroundColor = new Color(0.15f, 0.2f, 0.24f, 0.85f);
    [SerializeField] private Color equippedBackgroundColor = new Color(0.24f, 0.35f, 0.4f, 0.95f);
    [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Helmet;

    private EquipmentInventory equipmentInventory;
    private InventoryUI inventoryUI;

    public void Init(EquipmentInventory equipment, InventoryUI uiRef)
    {
        equipmentInventory = equipment;
        inventoryUI = uiRef;
        Refresh();
    }

    public void Refresh()
    {
        if (icon == null)
            return;

        InventoryItem slot = equipmentInventory != null ? equipmentInventory.GetItem((int)slotType) : null;
        if (slot == null || slot.IsEmpty || slot.item == null)
        {
            icon.enabled = false;
            icon.sprite = null;
            icon.color = Color.white;

            if (background != null && tintBackgroundByState)
                background.color = emptyBackgroundColor;
            return;
        }

        icon.enabled = slot.item.icon != null;
        icon.sprite = slot.item.icon;
        icon.color = Color.white;

        if (background != null && tintBackgroundByState)
            background.color = equippedBackgroundColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventoryUI == null || equipmentInventory == null || !inventoryUI.IsInventoryOpened)
            return;

        int slotIndex = (int)slotType;
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (IsShiftPressed())
            {
                inventoryUI.HandleFastTransfer(equipmentInventory, slotIndex);
                return;
            }

            inventoryUI.HandleSlotLeftClick(equipmentInventory, slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
            inventoryUI.HandleSlotRightClick(equipmentInventory, slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryUI == null || equipmentInventory == null)
            return;

        int slotIndex = (int)slotType;
        inventoryUI.SetHoveredSlot(equipmentInventory, slotIndex);

        InventoryItem slot = equipmentInventory.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item == null)
            return;

        inventoryUI.ShowTooltip(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI == null || equipmentInventory == null)
            return;

        int slotIndex = (int)slotType;
        inventoryUI.ClearHoveredSlot(equipmentInventory, slotIndex);
        inventoryUI.HideTooltip();
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }
}
