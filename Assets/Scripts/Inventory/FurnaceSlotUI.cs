using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FurnaceSlotUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image highlight;

    private FurnaceInventoryUI ownerUI;
    private int slotIndex;
    private ItemData displayedItem;

    public void Init(FurnaceInventoryUI owner, int index)
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
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (IsShiftPressed())
            {
                ownerUI?.HandleFastTransfer(slotIndex);
                return;
            }

            ownerUI?.HandleSlotLeftClick(slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
            ownerUI?.HandleSlotRightClick(slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlight(true);
        ownerUI?.SetHoveredSlot(slotIndex);
        if (displayedItem != null)
            ownerUI?.ShowTooltip(displayedItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlight(false);
        ownerUI?.ClearHoveredSlot(slotIndex);
        ownerUI?.HideTooltip();
    }

    private void SetHighlight(bool value)
    {
        if (highlight != null)
            highlight.enabled = value;
    }

    private void AutoBindIfNeeded()
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

    public int Index => slotIndex;
}
