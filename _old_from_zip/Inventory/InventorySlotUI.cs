using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image highlight;

    private int slotIndex;
    private Inventory inventory;
    private Transform dragParent;

    private static InventoryItem draggedItem;
    private static Image draggedIcon;
    private bool isDragging = false;

    private InventoryUI inventoryUI;

    public void Init(Inventory inv, InventoryUI ui, int index, Transform dragParentTransform)
    {
        inventory = inv;
        inventoryUI = ui;
        slotIndex = index;
        dragParent = dragParentTransform;
    }

    public void Refresh()
    {
        var item = inventory.GetItem(slotIndex);

        if (item == null || item.IsEmpty)
        {
            icon.enabled = false;
            amountText.text = "";
            return;
        }

        icon.enabled = true;
        icon.sprite = item.item.icon;

        Color c = icon.color;
        c.a = 1f;
        icon.color = c;

        amountText.text = item.amount > 0 ? item.amount.ToString() : "";
    }

    public void SetHighlight(bool value)
    {
        highlight.enabled = value;
    }

    #region Drag and Drop

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (inventoryUI != null &&
                (inventoryUI.IsChestOpenForTransfers() || inventoryUI.IsFurnaceOpenForTransfers()))
            {
                bool shiftPressed = IsShiftPressed();
                inventoryUI.TryTransferFromPlayerSlot(slotIndex, shiftPressed);
                return;
            }

            inventory.TryEquipFromSlot(slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left &&
            slotIndex < inventory.HotbarSize)
        {
            inventory.SetActiveSlot(slotIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var item = inventory.GetItem(slotIndex);
        if (item == null || item.IsEmpty) return;

        draggedItem = item;
        isDragging = true;

        if (draggedIcon == null)
        {
            draggedIcon = new GameObject("DragIcon").AddComponent<Image>();
            draggedIcon.raycastTarget = false;
            draggedIcon.transform.SetParent(dragParent, false);
            draggedIcon.rectTransform.sizeDelta = new Vector2(60, 60);
            draggedIcon.color = new Color(1f, 1f, 1f, 0.8f);
        }

        draggedIcon.sprite = item.item.icon;
        draggedIcon.enabled = true;

        UpdateDragPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || draggedItem == null || draggedIcon == null) return;
        UpdateDragPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || draggedItem == null || draggedIcon == null) return;

        isDragging = false;

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        InventorySlotUI targetSlot = null;
        ChestSlotUI targetChestSlot = null;
        FurnaceSlotUI targetFurnaceSlot = null;
        foreach (var r in results)
        {
            var slot = r.gameObject.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                targetSlot = slot;
                break;
            }

            var chestSlot = r.gameObject.GetComponent<ChestSlotUI>();
            if (chestSlot != null)
            {
                targetChestSlot = chestSlot;
                break;
            }

            var furnaceSlot = r.gameObject.GetComponent<FurnaceSlotUI>();
            if (furnaceSlot != null)
            {
                targetFurnaceSlot = furnaceSlot;
                break;
            }
        }

        if (targetSlot != null)
        {
            inventory.Swap(slotIndex, targetSlot.Index);
        }
        else if (targetChestSlot != null && inventoryUI != null)
        {
            inventoryUI.TryDropPlayerSlotToChestSlot(slotIndex, targetChestSlot.Index);
        }
        else if (targetFurnaceSlot != null && inventoryUI != null)
        {
            inventoryUI.TryDropPlayerSlotToFurnaceSlot(slotIndex, targetFurnaceSlot.Index);
        }

        draggedIcon.enabled = false;
        draggedItem = null;
    }

    private void UpdateDragPosition(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );
        draggedIcon.rectTransform.localPosition = localPoint;
    }

    #endregion

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    #region Tooltip
    public void OnPointerEnter(PointerEventData eventData)
    {
        var slot = inventory.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty) return;

        inventoryUI.ShowTooltip(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryUI.HideTooltip();
    }
    #endregion

    public int Index => slotIndex;
}
