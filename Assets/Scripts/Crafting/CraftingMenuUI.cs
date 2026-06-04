using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CraftingMenuUI : MonoBehaviour
{
    [SerializeField] private CraftingManager craftingManager;
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject recipeButtonPrefab;
    [SerializeField] private Transform recipesParent;
    [SerializeField] private Transform basicRecipesParent;
    [SerializeField] private Transform stationRecipesParent;
    [SerializeField] private GameObject categoryGroupPrefab;
    [SerializeField] private CraftRecipeTooltipPresenter tooltipPresenter;

    [Header("Panels")]
    [SerializeField] private GameObject basicCraftPanel;
    [SerializeField] private GameObject stationCraftPanel;
    [SerializeField] private bool includeBasicRecipesInStationContext;

    [Header("Legacy Category Controls (Optional)")]
    [SerializeField] private CraftingCategory currentCategory = CraftingCategory.All;

    private readonly List<CraftRecipeButton> buttons = new();
    private readonly List<CraftingCategoryGroupUI> categoryGroups = new();
    private readonly List<RaycastResult> pointerRaycastResults = new();
    private CraftStationType activeStationType = CraftStationType.None;
    private Coroutine showHoveredTooltipRoutine;

    void OnEnable()
    {
        UpdatePanelMode();
        BuildRecipes();
        RefreshButtons();
        ScheduleShowHoveredTooltip();

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshButtons;
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshButtons;

        HideTooltip();

        if (showHoveredTooltipRoutine != null)
        {
            StopCoroutine(showHoveredTooltipRoutine);
            showHoveredTooltipRoutine = null;
        }
    }

    public void Open(CraftStationType stationType)
    {
        activeStationType = stationType;
        UpdatePanelMode();

        if (!gameObject.activeSelf)
        {
            UIPanelJuice.SetVisible(gameObject, true);
            return;
        }

        BuildRecipes();
        RefreshButtons();
        ScheduleShowHoveredTooltip();
    }

    public void Close()
    {
        activeStationType = CraftStationType.None;
        HideTooltip();
        UpdatePanelMode();
        BuildRecipes();
        RefreshButtons();
    }

    public void HideTooltip()
    {
        tooltipPresenter?.Hide();
    }

    public void SetCategory(CraftingCategory category)
    {
        currentCategory = category;
        BuildRecipes();
        RefreshButtons();
    }

    public void SetCategoryByIndex(int categoryIndex)
    {
        if (!System.Enum.IsDefined(typeof(CraftingCategory), categoryIndex))
            return;

        SetCategory((CraftingCategory)categoryIndex);
    }

    public void BuildRecipes()
    {
        bool isStationContext = activeStationType != CraftStationType.None;
        if (recipeButtonPrefab == null || craftingManager == null)
            return;

        ClearBuiltUI();

        if (!isStationContext)
        {
            Transform targetRecipesParent = ResolveRecipesParent();
            if (targetRecipesParent == null)
                return;

            List<CraftingRecipe> basicRecipes = CollectRecipesForStation(CraftStationType.None);
            if (basicRecipes.Count == 0)
                return;

            BuildFlatRecipes(basicRecipes, targetRecipesParent);
            ScheduleShowHoveredTooltip();
            return;
        }

        Transform basicParent = basicRecipesParent != null ? basicRecipesParent : recipesParent;
        if (basicParent != null && !includeBasicRecipesInStationContext)
        {
            List<CraftingRecipe> basicRecipes = CollectRecipesForStation(CraftStationType.None);
            BuildFlatRecipes(basicRecipes, basicParent);
        }

        Transform stationParent = stationRecipesParent != null ? stationRecipesParent : recipesParent;
        if (stationParent == null)
            return;

        List<CraftingRecipe> availableStationRecipes = CollectRecipesForActiveStationContext();
        if (availableStationRecipes.Count == 0)
            return;

        if (categoryGroupPrefab == null)
        {
            Debug.LogWarning("CraftingMenuUI: categoryGroupPrefab is not assigned for station crafting.");
            BuildFlatRecipes(availableStationRecipes, stationParent);
            ScheduleShowHoveredTooltip();
            return;
        }

        BuildGroupedRecipes(availableStationRecipes, stationParent);
        ScheduleShowHoveredTooltip();
    }

    private void BuildFlatRecipes(List<CraftingRecipe> recipes, Transform parent)
    {
        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipe recipe = recipes[i];
            if (recipe == null)
                continue;

            GameObject recipeButtonObject = Instantiate(recipeButtonPrefab, parent);
            CraftRecipeButton button = recipeButtonObject.GetComponent<CraftRecipeButton>();
            if (button == null)
            {
                Destroy(recipeButtonObject);
                continue;
            }

            button.Setup(recipe, craftingManager, activeStationType);
            button.SetTooltipPresenter(tooltipPresenter);
            buttons.Add(button);
        }
    }

    private void BuildGroupedRecipes(List<CraftingRecipe> recipes, Transform parent)
    {
        Dictionary<CraftingCategory, List<CraftingRecipe>> groupedRecipes = GroupRecipesByCategory(recipes);

        Array orderedCategories = Enum.GetValues(typeof(CraftingCategory));
        for (int i = 0; i < orderedCategories.Length; i++)
        {
            CraftingCategory category = (CraftingCategory)orderedCategories.GetValue(i);
            if (!groupedRecipes.TryGetValue(category, out List<CraftingRecipe> recipesInCategory) ||
                recipesInCategory == null ||
                recipesInCategory.Count == 0)
                continue;

            CraftingCategoryGroupUI group = CreateCategoryGroup(category, parent);
        if (group == null)
            continue;

            Transform recipesParentForCategory = group.RecipesParent;
            for (int recipeIndex = 0; recipeIndex < recipesInCategory.Count; recipeIndex++)
            {
                CraftingRecipe recipe = recipesInCategory[recipeIndex];
                if (recipe == null)
                    continue;

                GameObject recipeButtonObject = Instantiate(recipeButtonPrefab, recipesParentForCategory);
                CraftRecipeButton button = recipeButtonObject.GetComponent<CraftRecipeButton>();
                if (button == null)
                {
                    Destroy(recipeButtonObject);
                    continue;
                }

                button.Setup(recipe, craftingManager, activeStationType);
                button.SetTooltipPresenter(tooltipPresenter);
                buttons.Add(button);
            }
        }
    }

    public void RefreshButtons()
    {
        foreach (var btn in buttons)
            btn.Refresh();
    }

    private void UpdatePanelMode()
    {
        bool isStationContext = activeStationType != CraftStationType.None;

        if (basicCraftPanel != null)
            basicCraftPanel.SetActive(true);

        if (stationCraftPanel != null)
            stationCraftPanel.SetActive(isStationContext);
    }

    private Transform ResolveRecipesParent()
    {
        bool isStationContext = activeStationType != CraftStationType.None;

        if (isStationContext && stationRecipesParent != null)
            return stationRecipesParent;

        if (!isStationContext && basicRecipesParent != null)
            return basicRecipesParent;

        return recipesParent;
    }

    private List<CraftingRecipe> CollectRecipesForStation(CraftStationType stationType)
    {
        List<CraftingRecipe> visible = new();

        IReadOnlyList<CraftingRecipe> allRecipes = craftingManager.AllRecipes;
        for (int i = 0; i < allRecipes.Count; i++)
        {
            CraftingRecipe recipe = allRecipes[i];
            if (recipe == null || recipe.station != stationType)
                continue;

            visible.Add(recipe);
        }

        return visible;
    }

    private List<CraftingRecipe> CollectRecipesForActiveStationContext()
    {
        List<CraftingRecipe> visible = new();

        IReadOnlyList<CraftingRecipe> allRecipes = craftingManager.AllRecipes;
        for (int i = 0; i < allRecipes.Count; i++)
        {
            CraftingRecipe recipe = allRecipes[i];
            if (recipe == null)
                continue;

            if (recipe.station == activeStationType ||
                (includeBasicRecipesInStationContext && recipe.station == CraftStationType.None))
            {
                visible.Add(recipe);
            }
        }

        return visible;
    }

    private static Dictionary<CraftingCategory, List<CraftingRecipe>> GroupRecipesByCategory(List<CraftingRecipe> recipes)
    {
        Dictionary<CraftingCategory, List<CraftingRecipe>> grouped = new();

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipe recipe = recipes[i];
            if (recipe == null)
                continue;

            if (!grouped.TryGetValue(recipe.category, out List<CraftingRecipe> recipesInCategory))
            {
                recipesInCategory = new List<CraftingRecipe>();
                grouped.Add(recipe.category, recipesInCategory);
            }

            recipesInCategory.Add(recipe);
        }

        return grouped;
    }

    private CraftingCategoryGroupUI CreateCategoryGroup(CraftingCategory category, Transform parent)
    {
        GameObject groupObject = Instantiate(categoryGroupPrefab, parent);
        CraftingCategoryGroupUI group = groupObject.GetComponent<CraftingCategoryGroupUI>();
        if (group == null)
        {
            Debug.LogWarning("CraftingMenuUI: categoryGroupPrefab must have CraftingCategoryGroupUI.");
            Destroy(groupObject);
            return null;
        }

        group.Setup(category);
        categoryGroups.Add(group);
        return group;
    }

    private void ClearBuiltUI()
    {
        for (int i = 0; i < categoryGroups.Count; i++)
        {
            if (categoryGroups[i] != null)
                Destroy(categoryGroups[i].gameObject);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
                Destroy(buttons[i].gameObject);
        }

        categoryGroups.Clear();
        buttons.Clear();
    }

    private void ScheduleShowHoveredTooltip()
    {
        if (!isActiveAndEnabled)
            return;

        if (showHoveredTooltipRoutine != null)
            StopCoroutine(showHoveredTooltipRoutine);

        showHoveredTooltipRoutine = StartCoroutine(ShowHoveredTooltipNextFrame());
    }

    private System.Collections.IEnumerator ShowHoveredTooltipNextFrame()
    {
        yield return null;
        showHoveredTooltipRoutine = null;
        ShowHoveredTooltipUnderCursor();
    }

    private void ShowHoveredTooltipUnderCursor()
    {
        if (tooltipPresenter == null || EventSystem.current == null)
            return;

        Vector2 pointerPosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };

        pointerRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, pointerRaycastResults);

        for (int i = 0; i < pointerRaycastResults.Count; i++)
        {
            CraftRecipeButton button = pointerRaycastResults[i].gameObject.GetComponentInParent<CraftRecipeButton>();
            if (button == null || !buttons.Contains(button))
                continue;

            button.ShowTooltip();
            return;
        }
    }
}
