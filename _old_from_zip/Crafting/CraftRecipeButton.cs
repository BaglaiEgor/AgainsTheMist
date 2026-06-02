using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftRecipeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private CraftingRecipe recipe;
    private CraftingManager manager;
    private CraftRecipeTooltipPresenter tooltipPresenter;
    private CraftStationType stationContext = CraftStationType.None;

    public void Setup(CraftingRecipe recipe, CraftingManager manager)
    {
        Setup(recipe, manager, CraftStationType.None);
    }

    public void Setup(CraftingRecipe recipe, CraftingManager manager, CraftStationType stationContext)
    {
        this.recipe = recipe;
        this.manager = manager;
        this.stationContext = stationContext;

        if (icon != null)
            icon.sprite = recipe.result != null ? recipe.result.icon : null;

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        Refresh();
    }

    public void SetTooltipPresenter(CraftRecipeTooltipPresenter presenter)
    {
        tooltipPresenter = presenter;
    }

    public void Refresh()
    {
        if (button == null)
            return;

        button.interactable = manager != null &&
                             recipe != null &&
                             manager.CanCraft(recipe, stationContext);
    }

    private void OnClick()
    {
        if (manager == null || recipe == null)
            return;

        manager.Craft(recipe, stationContext);
        Refresh();
    }

    private void OnDisable()
    {
        if (tooltipPresenter != null)
            tooltipPresenter.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPresenter == null || recipe == null)
            return;

        tooltipPresenter.Show(recipe, manager);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPresenter != null)
            tooltipPresenter.Hide();
    }

    public CraftingRecipe Recipe => recipe;
    public CraftingManager Manager => manager;
}
