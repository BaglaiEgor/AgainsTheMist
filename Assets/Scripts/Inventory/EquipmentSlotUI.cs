using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

public class EquipmentSlotUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [SerializeField] private bool tintBackgroundByState;
    [SerializeField] private Color emptyBackgroundColor = new Color(0.15f, 0.2f, 0.24f, 0.85f);
    [SerializeField] private Color equippedBackgroundColor = new Color(0.24f, 0.35f, 0.4f, 0.95f);
    [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Helmet;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float hoverDuration = 0.1f;
    [SerializeField] private float clickPunchScale = 0.08f;

    private EquipmentInventory equipmentInventory;
    private InventoryUI inventoryUI;
    private Tween scaleTween;
    private Vector3 baseScale = Vector3.one;
    private bool isHovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

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
            PlayClickFeedback();

            if (IsShiftPressed())
            {
                inventoryUI.HandleFastTransfer(equipmentInventory, slotIndex);
                return;
            }

            inventoryUI.HandleSlotLeftClick(equipmentInventory, slotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            PlayClickFeedback();
            inventoryUI.HandleSlotRightClick(equipmentInventory, slotIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryUI == null || equipmentInventory == null)
            return;

        int slotIndex = (int)slotType;
        isHovered = true;
        AnimateScale(hoverScale);
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
        isHovered = false;
        AnimateScale(1f);
        inventoryUI.ClearHoveredSlot(equipmentInventory, slotIndex);
        inventoryUI.HideTooltip();
    }

    private void OnDisable()
    {
        isHovered = false;
        KillScaleTween();
        transform.localScale = baseScale;
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
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
}
