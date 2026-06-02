using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EquipmentSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [SerializeField] private bool tintBackgroundByState;
    [SerializeField] private Color emptyBackgroundColor = new Color(0.15f, 0.2f, 0.24f, 0.85f);
    [SerializeField] private Color equippedBackgroundColor = new Color(0.24f, 0.35f, 0.4f, 0.95f);

    private Inventory inventory;
    private InventoryUI inventoryUI;

    public void Init(Inventory inventoryRef, InventoryUI uiRef)
    {
        inventory = inventoryRef;
        inventoryUI = uiRef;

        if (icon == null)
            Debug.LogWarning("EquipmentSlotUI: icon is not assigned in Inspector.");
        if (background == null)
            Debug.LogWarning("EquipmentSlotUI: background is not assigned in Inspector.");

        Refresh();
    }

    public void Refresh()
    {
        if (icon == null)
            return;

        ItemData equippedItem = inventory != null ? inventory.EquippedItem : null;
        if (equippedItem == null)
        {
            icon.enabled = false;
            icon.sprite = null;
            if (background != null && tintBackgroundByState)
                background.color = emptyBackgroundColor;
            return;
        }

        icon.enabled = equippedItem.icon != null;
        icon.sprite = equippedItem.icon;
        icon.color = Color.white;

        if (background != null && tintBackgroundByState)
            background.color = equippedBackgroundColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryUI == null || inventory == null)
            return;

        ItemData equippedItem = inventory.EquippedItem;
        if (equippedItem == null)
            return;

        inventoryUI.ShowTooltip(equippedItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI == null)
            return;

        inventoryUI.HideTooltip();
    }
}
