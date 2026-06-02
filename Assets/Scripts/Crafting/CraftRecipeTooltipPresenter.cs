using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CraftRecipeTooltipPresenter : MonoBehaviour
{
    [Header("Tooltip UI")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TextMeshProUGUI resultNameText;
    [SerializeField] private TextMeshProUGUI resultDescriptionText;
    [SerializeField] private TextMeshProUGUI resultExtraText;
    [SerializeField] private TextMeshProUGUI resultAmountText;
    [SerializeField] private RectTransform ingredientsRoot;
    [SerializeField] private TextMeshProUGUI ingredientsFallbackText;

    [Header("Visual")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(20f, 20f);
    [SerializeField] private Vector2 ingredientIconSize = new Vector2(18f, 18f);
    [SerializeField] private float ingredientRowSpacing = 6f;
    [SerializeField] private float maxContentWidth = 260f;
    [SerializeField] private int tooltipSortingOrder = 1000;
    [SerializeField] private Color enoughColor = new Color(0.45f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color missingColor = new Color(0.95f, 0.4f, 0.4f, 1f);

    private readonly List<GameObject> runtimeIngredientRows = new List<GameObject>();

    private CraftingRecipe recipe;
    private Inventory inventory;
    private Canvas canvas;
    private Canvas tooltipCanvas;
    private RectTransform canvasRect;
    private RectTransform tooltipParentRect;
    private bool tooltipVisible;
    private bool subscribedToInventory;
    private bool initialized;

    private void Awake()
    {
        InitializeIfNeeded();
        HideTooltipInternal();
    }

    private void OnDisable()
    {
        HideTooltipInternal();
    }

    private void Update()
    {
        if (!tooltipVisible)
            return;

        FollowCursor();
    }

    public void Show(CraftingRecipe recipeToShow, CraftingManager managerFromButton)
    {
        InitializeIfNeeded();

        if (recipeToShow == null || tooltipRoot == null)
            return;

        UnsubscribeFromInventory();

        recipe = recipeToShow;
        inventory = managerFromButton != null ? managerFromButton.PlayerInventory : null;

        BuildTooltip();
        ShowTooltipInternal();
    }

    public void Hide()
    {
        HideTooltipInternal();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        if (tooltipRoot == null)
            tooltipRoot = transform as RectTransform;

        CacheCanvas();
        ConfigureTooltipRoot();
        initialized = true;
    }

    private void ConfigureTooltipRoot()
    {
        if (tooltipRoot == null)
            return;

        tooltipRoot.localScale = Vector3.one;

        ContentSizeFitter tooltipFitter = tooltipRoot.GetComponent<ContentSizeFitter>();
        if (tooltipFitter != null)
            tooltipFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        tooltipCanvas = tooltipRoot.GetComponent<Canvas>();
        if (tooltipCanvas == null)
            tooltipCanvas = tooltipRoot.gameObject.AddComponent<Canvas>();

        tooltipCanvas.overrideSorting = true;
        if (canvas != null)
            tooltipCanvas.sortingLayerID = canvas.sortingLayerID;
        tooltipCanvas.sortingOrder = tooltipSortingOrder;

        if (resultNameText != null)
        {
            resultNameText.overflowMode = TextOverflowModes.Overflow;
            resultNameText.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    private void BuildTooltip()
    {
        ItemData resultItem = recipe != null ? recipe.result : null;

        if (resultNameText != null)
            resultNameText.text = TextMarkupParser.Parse(resultItem != null ? resultItem.itemName : "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u044b\u0439 \u0440\u0435\u0437\u0443\u043b\u044c\u0442\u0430\u0442");

        if (resultDescriptionText != null)
        {
            string description = resultItem != null ? resultItem.description : string.Empty;
            resultDescriptionText.text = TextMarkupParser.Parse(description);
            resultDescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }

        if (resultExtraText != null)
        {
            string extra = BuildItemExtra(resultItem);
            resultExtraText.text = extra;
            resultExtraText.gameObject.SetActive(!string.IsNullOrEmpty(extra));
        }

        if (resultAmountText != null)
            resultAmountText.text = "\u0418\u0442\u043e\u0433: " + Mathf.Max(1, recipe.resultAmount);

        BuildIngredients();
    }

    private void BuildIngredients()
    {
        ClearIngredientRows();
        bool canUseFallbackText = ingredientsFallbackText != null && ingredientsFallbackText != resultExtraText;

        if (recipe == null || recipe.ingredients == null || recipe.ingredients.Length == 0)
        {
            if (canUseFallbackText)
            {
                ingredientsFallbackText.text = "\u041d\u0435\u0442 \u0438\u043d\u0433\u0440\u0435\u0434\u0438\u0435\u043d\u0442\u043e\u0432";
                ingredientsFallbackText.color = enoughColor;
            }
            return;
        }

        if (ingredientsRoot == null)
        {
            if (canUseFallbackText)
                BuildFallbackIngredientsText();
            return;
        }

        if (canUseFallbackText)
            ingredientsFallbackText.text = string.Empty;

        for (int i = 0; i < recipe.ingredients.Length; i++)
            CreateIngredientRow(recipe.ingredients[i]);
    }

    private void BuildFallbackIngredientsText()
    {
        if (ingredientsFallbackText == null || recipe == null || recipe.ingredients == null)
            return;

        System.Text.StringBuilder lines = new System.Text.StringBuilder();

        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            CraftingRecipe.Ingredient ingredient = recipe.ingredients[i];
            int current = CountInInventory(ingredient.item);
            int need = Mathf.Max(0, ingredient.amount);

            string colorHex = ColorUtility.ToHtmlStringRGB(current >= need ? enoughColor : missingColor);
            string itemName = ingredient.item != null ? ingredient.item.itemName : "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u044b\u0439 \u0438\u043d\u0433\u0440\u0435\u0434\u0438\u0435\u043d\u0442";
            lines.Append($"<color=#{colorHex}>{itemName} {current}/{need}</color>");

            if (i < recipe.ingredients.Length - 1)
                lines.AppendLine();
        }

        ingredientsFallbackText.text = lines.ToString();
    }

    private static string BuildItemExtra(ItemData item)
    {
        if (item == null)
            return string.Empty;

        switch (item.type)
        {
            case ItemType.Weapon:
                return "Урон: " + item.damage;
            case ItemType.Tool:
                return "Эффективность: " + item.toolPower;
            case ItemType.Structure:
                return "Можно поставить";
            case ItemType.Material:
                return "Материал";
            case ItemType.Seed:
                return "Можно посадить";
            case ItemType.Lantern:
                return "Заряд: " + Mathf.Max(0f, item.lanternMaxCharge).ToString("0");
            case ItemType.Equipment:
                float frostMul = Mathf.Max(0f, item.frostDamageMultiplier);
                float pressureMul = Mathf.Max(0f, item.pressureGrowthMultiplier);
                return "Экипировка\n" +
                       "Морозный урон x" + frostMul.ToString("0.##") + "\n" +
                       "Рост давления x" + pressureMul.ToString("0.##");
            case ItemType.Food:
                return "Еда: +" + Mathf.Max(0, item.foodRestore);
            default:
                return string.Empty;
        }
    }

    private void CreateIngredientRow(CraftingRecipe.Ingredient ingredient)
    {
        if (ingredientsRoot == null)
            return;

        int current = CountInInventory(ingredient.item);
        int need = Mathf.Max(0, ingredient.amount);
        bool enough = current >= need;

        GameObject row = new GameObject("IngredientRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(ingredientsRoot, false);
        runtimeIngredientRows.Add(row);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = ingredientRowSpacing;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(row.transform, false);

        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = ingredient.item != null ? ingredient.item.icon : null;
        iconImage.enabled = iconImage.sprite != null;
        iconImage.preserveAspect = true;

        LayoutElement iconLayout = iconGo.GetComponent<LayoutElement>();
        Vector2 iconSize = new Vector2(
            Mathf.Max(1f, ingredientIconSize.x),
            Mathf.Max(1f, ingredientIconSize.y)
        );
        iconLayout.preferredWidth = iconSize.x;
        iconLayout.preferredHeight = iconSize.y;
        iconLayout.minWidth = iconSize.x;
        iconLayout.minHeight = iconSize.y;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(row.transform, false);

        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = enough ? enoughColor : missingColor;
        text.fontSize = ResolveIngredientFontSize();
        text.alignment = TextAlignmentOptions.Left;
        text.text = BuildIngredientLabel(ingredient, current, need);

        if (ingredientsFallbackText != null && ingredientsFallbackText.font != null)
            text.font = ingredientsFallbackText.font;

        LayoutElement textLayout = textGo.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
    }

    private static string BuildIngredientLabel(CraftingRecipe.Ingredient ingredient, int current, int need)
    {
        string name = ingredient.item != null ? ingredient.item.itemName : "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u044b\u0439 \u0438\u043d\u0433\u0440\u0435\u0434\u0438\u0435\u043d\u0442";
        return $"{name} {current}/{need}";
    }

    private float ResolveIngredientFontSize()
    {
        if (ingredientsFallbackText != null)
            return ingredientsFallbackText.fontSize;

        if (resultAmountText != null)
            return resultAmountText.fontSize;

        return TMP_Settings.defaultFontSize;
    }

    private int CountInInventory(ItemData item)
    {
        if (inventory == null || item == null)
            return 0;

        int total = 0;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventoryItem slot = inventory.GetItem(i);
            if (slot == null || slot.IsEmpty || slot.item != item)
                continue;

            total += slot.amount;
        }

        return total;
    }

    private void ShowTooltipInternal()
    {
        if (tooltipRoot == null)
            return;

        tooltipVisible = true;
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        ApplyMaxContentWidth();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
        FollowCursor();
        SubscribeToInventory();
    }

    private void HideTooltipInternal()
    {
        tooltipVisible = false;

        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);

        UnsubscribeFromInventory();
        ClearIngredientRows();

        if (ingredientsFallbackText != null)
            ingredientsFallbackText.text = string.Empty;
    }

    private void FollowCursor()
    {
        if (tooltipRoot == null)
            return;

        Vector2 screenPoint = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        RectTransform targetSpace = tooltipParentRect != null ? tooltipParentRect : canvasRect;
        if (targetSpace == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetSpace,
            screenPoint,
            eventCamera,
            out Vector2 localPoint
        );

        Vector2 pivotFromBottomLeft = new Vector2(
            tooltipRoot.rect.width * tooltipRoot.pivot.x,
            tooltipRoot.rect.height * tooltipRoot.pivot.y
        );

        tooltipRoot.localPosition = localPoint + cursorOffset + pivotFromBottomLeft;
    }

    private void CacheCanvas()
    {
        if (tooltipRoot != null)
            tooltipParentRect = tooltipRoot.parent as RectTransform;

        if (tooltipRoot != null)
            canvas = tooltipRoot.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    private void SubscribeToInventory()
    {
        if (inventory == null || subscribedToInventory)
            return;

        inventory.OnInventoryChanged += HandleInventoryChanged;
        subscribedToInventory = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (inventory == null || !subscribedToInventory)
            return;

        inventory.OnInventoryChanged -= HandleInventoryChanged;
        subscribedToInventory = false;
    }

    private void HandleInventoryChanged()
    {
        if (!tooltipVisible || recipe == null)
            return;

        BuildIngredients();
        ApplyMaxContentWidth();

        if (tooltipRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
    }

    private void ApplyMaxContentWidth()
    {
        if (tooltipRoot == null)
            return;

        float widthLimit = Mathf.Max(1f, maxContentWidth);
        float minWidthFromName = 1f;
        if (resultNameText != null && resultNameText.gameObject.activeSelf)
        {
            resultNameText.ForceMeshUpdate();
            minWidthFromName = Mathf.Max(1f, resultNameText.GetPreferredValues(resultNameText.text).x + 6f);
        }

        if (widthLimit < minWidthFromName)
            widthLimit = minWidthFromName;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);

        float preferredWidth = LayoutUtility.GetPreferredWidth(tooltipRoot);
        float targetWidth = Mathf.Clamp(preferredWidth, minWidthFromName, widthLimit);
        tooltipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private void ClearIngredientRows()
    {
        for (int i = 0; i < runtimeIngredientRows.Count; i++)
        {
            if (runtimeIngredientRows[i] != null)
                Destroy(runtimeIngredientRows[i]);
        }

        runtimeIngredientRows.Clear();
    }
}
