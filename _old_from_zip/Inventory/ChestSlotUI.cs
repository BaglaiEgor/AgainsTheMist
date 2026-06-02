using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChestSlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image highlight;

    private ChestInventoryUI ownerUI;
    private int slotIndex;
    private ItemData displayedItem;
    private bool isDragging;

    private static Image draggedIcon;

    public void Init(ChestInventoryUI owner, int index)
    {
        ownerUI = owner;
        slotIndex = index;
        AutoBindIfNeeded();
        SetHighlight(false);
    }

    public void Refresh(InventoryItem item)
    {
        AutoBindIfNeeded();

        displayedItem = item != null && !item.IsEmpty ? item.item : null;
        if (displayedItem == null || item == null || item.amount <= 0)
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
            icon.sprite = displayedItem.icon;
            icon.color = Color.white;
        }

        if (amountText != null)
            amountText.text = item.amount > 0 ? item.amount.ToString() : string.Empty;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        bool shiftPressed = IsShiftPressed();
        ownerUI?.TryTransferFromChestSlot(slotIndex, shiftPressed);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        InventoryItem slot = ownerUI?.CurrentChest?.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item == null)
            return;

        Transform dragParent = ownerUI?.GetDragParent();
        if (dragParent == null)
            return;

        isDragging = true;

        if (draggedIcon == null)
        {
            draggedIcon = new GameObject("ChestDragIcon").AddComponent<Image>();
            draggedIcon.raycastTarget = false;
            draggedIcon.transform.SetParent(dragParent, false);
            draggedIcon.rectTransform.sizeDelta = new Vector2(60, 60);
            draggedIcon.color = new Color(1f, 1f, 1f, 0.8f);
        }

        draggedIcon.sprite = slot.item.icon;
        draggedIcon.enabled = true;

        UpdateDragPosition(eventData, dragParent);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || draggedIcon == null)
            return;

        Transform dragParent = ownerUI?.GetDragParent();
        if (dragParent == null)
            return;

        UpdateDragPosition(eventData, dragParent);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        ChestSlotUI targetChestSlot = null;
        InventorySlotUI targetInventorySlot = null;

        for (int i = 0; i < results.Count; i++)
        {
            RaycastResult result = results[i];
            if (targetChestSlot == null)
                targetChestSlot = result.gameObject.GetComponent<ChestSlotUI>();

            if (targetInventorySlot == null)
                targetInventorySlot = result.gameObject.GetComponent<InventorySlotUI>();

            if (targetChestSlot != null || targetInventorySlot != null)
                break;
        }

        if (targetChestSlot != null)
        {
            ownerUI?.TrySwapChestSlots(slotIndex, targetChestSlot.Index);
        }
        else if (targetInventorySlot != null)
        {
            ownerUI?.TryDropChestSlotToPlayerSlot(slotIndex, targetInventorySlot.Index);
        }

        if (draggedIcon != null)
            draggedIcon.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlight(true);
        if (displayedItem != null)
            ownerUI?.ShowTooltip(displayedItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlight(false);
        ownerUI?.HideTooltip();
    }

    void SetHighlight(bool value)
    {
        if (highlight != null)
            highlight.enabled = value;
    }

    void AutoBindIfNeeded()
    {
        if (icon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                icon = iconTransform.GetComponent<Image>();
        }

        if (amountText == null)
            amountText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (highlight == null)
        {
            Transform highlightTransform = transform.Find("Highlight");
            if (highlightTransform != null)
                highlight = highlightTransform.GetComponent<Image>();
        }
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    private static void UpdateDragPosition(PointerEventData eventData, Transform dragParent)
    {
        if (draggedIcon == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        draggedIcon.rectTransform.localPosition = localPoint;
    }

    public int Index => slotIndex;
}
