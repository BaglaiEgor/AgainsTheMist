using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LighthouseKeeperRecipeCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI titleText;

    private CraftingRecipe recipe;
    private ItemData item;
    private CraftingManager craftingManager;
    private CraftRecipeTooltipPresenter tooltipPresenter;
    private ItemTooltip itemTooltip;
    private Action<ItemData> itemClickCallback;

    public void BindViews(Image icon, TextMeshProUGUI titleText)
    {
        this.icon = icon;
        this.titleText = titleText;
    }

    public void Setup(CraftingRecipe recipe, CraftingManager craftingManager, CraftRecipeTooltipPresenter tooltipPresenter)
    {
        this.recipe = recipe;
        item = null;
        this.craftingManager = craftingManager;
        this.tooltipPresenter = tooltipPresenter;
        itemTooltip = null;
        itemClickCallback = null;

        gameObject.SetActive(recipe != null);

        ItemData result = recipe != null ? recipe.result : null;
        if (icon != null)
        {
            icon.sprite = result != null ? result.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (titleText != null)
        {
            string name = result != null ? result.itemName : "Неизвестный рецепт";
            int amount = recipe != null ? Mathf.Max(1, recipe.resultAmount) : 1;
            titleText.text = amount > 1 ? $"{name} x{amount}" : name;
        }
    }

    public void SetupItem(ItemData item, ItemTooltip tooltip, Action<ItemData> onClick = null)
    {
        recipe = null;
        this.item = item;
        craftingManager = null;
        tooltipPresenter = null;
        itemTooltip = tooltip;
        itemClickCallback = onClick;

        gameObject.SetActive(item != null);

        if (icon != null)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (titleText != null)
            titleText.text = item != null ? item.itemName : string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (tooltipPresenter != null && recipe != null)
            tooltipPresenter.Show(recipe, craftingManager);

        if (itemTooltip != null && item != null)
            itemTooltip.Show(item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPresenter != null)
            tooltipPresenter.Hide();

        if (itemTooltip != null)
            itemTooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (item != null)
            itemClickCallback?.Invoke(item);
    }
}
