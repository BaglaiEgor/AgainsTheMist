using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image highlight;

    private int slotIndex;
    private Inventory inventory;
    private InventoryUI inventoryUI;

    public void Init(Inventory inv, InventoryUI ui, int index, Transform _unusedDragParentTransform)
    {
        inventory = inv;
        inventoryUI = ui;
        slotIndex = index;
    }

    public void Refresh()
    {
        InventoryItem item = inventory != null ? inventory.GetItem(slotIndex) : null;

        if (item == null || item.IsEmpty || item.item == null)
        {
            if (icon != null)
            {
                icon.enabled = false;
                icon.sprite = null;
            }

            if (amountText != null)
                amountText.text = string.Empty;

            return;
        }

        if (icon != null)
        {
            icon.enabled = true;
            icon.sprite = item.item.icon;
            bool isSourceSlot = inventoryUI != null && inventoryUI.IsCursorSourceSlot(inventory, slotIndex);
            icon.color = isSourceSlot
                ? new Color(1f, 1f, 1f, 0.45f)
                : Color.white;
        }

        if (amountText != null)
            amountText.text = item.amount > 0 ? item.amount.ToString() : string.Empty;
    }

    public void SetHighlight(bool value)
    {
        if (highlight != null)
            highlight.enabled = value;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inventory == null || inventoryUI == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!inventoryUI.IsInventoryOpened)
            {
                if (slotIndex < inventory.HotbarSize)
                    inventory.SetActiveSlot(slotIndex);
                return;
            }

            if (IsShiftPressed())
            {
                inventoryUI.HandleFastTransfer(inventory, slotIndex);
                return;
            }

            inventoryUI.HandleSlotLeftClick(inventory, slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!inventoryUI.IsInventoryOpened)
                return;

            inventoryUI.HandleSlotRightClick(inventory, slotIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inventoryUI?.SetHoveredSlot(inventory, slotIndex);

        InventoryItem slot = inventory != null ? inventory.GetItem(slotIndex) : null;
        if (slot == null || slot.IsEmpty || slot.item == null)
            return;

        inventoryUI?.ShowTooltip(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryUI?.ClearHoveredSlot(inventory, slotIndex);
        inventoryUI?.HideTooltip();
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    public int Index => slotIndex;
}
