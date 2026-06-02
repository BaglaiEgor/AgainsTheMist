using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LighthouseKeeperItemCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Button button;

    private ItemData item;
    private ItemTooltip tooltip;
    private Action<ItemData> onClick;

    public void BindViews(Image icon, TextMeshProUGUI amountText, Button button)
    {
        this.icon = icon;
        this.amountText = amountText;
        this.button = button;
    }

    public void Setup(ItemData item, ItemTooltip tooltip, Action<ItemData> onClick, int amount = 0)
    {
        this.item = item;
        this.tooltip = tooltip;
        this.onClick = onClick;

        gameObject.SetActive(item != null);

        if (icon != null)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (amountText != null)
            amountText.text = amount > 1 ? amount.ToString() : string.Empty;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (item != null)
            onClick?.Invoke(item);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (tooltip != null && item != null)
            tooltip.Show(item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null)
            tooltip.Hide();
    }
}
