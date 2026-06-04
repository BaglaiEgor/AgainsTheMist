using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image highlight;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float hoverDuration = 0.1f;
    [SerializeField] private float clickPunchScale = 0.08f;

    private int slotIndex;
    private Inventory inventory;
    private InventoryUI inventoryUI;
    private Tween scaleTween;
    private Vector3 baseScale = Vector3.one;
    private bool isHovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

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
                {
                    PlayClickFeedback();
                    inventory.SetActiveSlot(slotIndex);
                }
                return;
            }

            if (IsShiftPressed())
            {
                PlayClickFeedback();
                inventoryUI.HandleFastTransfer(inventory, slotIndex);
                return;
            }

            PlayClickFeedback();
            inventoryUI.HandleSlotLeftClick(inventory, slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!inventoryUI.IsInventoryOpened)
                return;

            PlayClickFeedback();
            inventoryUI.HandleSlotRightClick(inventory, slotIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        AnimateScale(hoverScale);
        inventoryUI?.SetHoveredSlot(inventory, slotIndex);

        InventoryItem slot = inventory != null ? inventory.GetItem(slotIndex) : null;
        if (slot == null || slot.IsEmpty || slot.item == null)
            return;

        inventoryUI?.ShowTooltip(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        AnimateScale(1f);
        inventoryUI?.ClearHoveredSlot(inventory, slotIndex);
        inventoryUI?.HideTooltip();
    }

    private void OnDisable()
    {
        isHovered = false;
        KillScaleTween();
        transform.localScale = baseScale;
    }

    private void AnimateScale(float targetScale)
    {
        KillScaleTween();
        scaleTween = transform
            .DOScale(baseScale * Mathf.Max(0.01f, targetScale), Mathf.Max(0.01f, hoverDuration))
            .SetEase(Ease.OutQuad);
    }

    private void PlayClickFeedback()
    {
        KillScaleTween();
        transform.localScale = baseScale * (isHovered ? Mathf.Max(0.01f, hoverScale) : 1f);
        scaleTween = transform
            .DOPunchScale(baseScale * Mathf.Max(0f, clickPunchScale), 0.12f, 6, 0.45f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => transform.localScale = baseScale * (isHovered ? Mathf.Max(0.01f, hoverScale) : 1f));
    }

    private void KillScaleTween()
    {
        if (scaleTween == null)
            return;

        scaleTween.Kill();
        scaleTween = null;
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
