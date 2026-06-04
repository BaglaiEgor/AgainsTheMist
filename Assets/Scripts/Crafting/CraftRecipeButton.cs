using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.UI;

public class CraftRecipeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Button button;
    [SerializeField] private float hoverScale = 1.03f;
    [SerializeField] private float hoverDuration = 0.1f;
    [SerializeField] private float clickPunchScale = 0.08f;

    private CraftingRecipe recipe;
    private CraftingManager manager;
    private CraftRecipeTooltipPresenter tooltipPresenter;
    private CraftStationType stationContext = CraftStationType.None;
    private Tween scaleTween;
    private Vector3 baseScale = Vector3.one;
    private bool isHovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

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

        PlayClickFeedback();

        if (IsControlPressed())
            manager.CraftToInventory(recipe, stationContext);
        else if (IsShiftPressed())
            manager.CraftMax(recipe, stationContext);
        else
            manager.Craft(recipe, stationContext);

        Refresh();
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    private static bool IsControlPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
    }

    private void OnDisable()
    {
        isHovered = false;
        KillScaleTween();
        transform.localScale = baseScale;

        if (tooltipPresenter != null)
            tooltipPresenter.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (IsButtonInteractable())
            AnimateScale(hoverScale);

        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        AnimateScale(1f);

        HideTooltip();
    }

    public void ShowTooltip()
    {
        if (tooltipPresenter == null || recipe == null)
            return;

        tooltipPresenter.Show(recipe, manager);
    }

    public void HideTooltip()
    {
        if (tooltipPresenter != null)
            tooltipPresenter.Hide();
    }

    public CraftingRecipe Recipe => recipe;
    public CraftingManager Manager => manager;

    private bool IsButtonInteractable()
    {
        return button == null || button.interactable;
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
        if (!IsButtonInteractable())
            return;

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
