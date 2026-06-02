using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemTooltip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI extraText;

    [SerializeField] private Vector2 offset = new Vector2(20, 20);
    [SerializeField] private float maxContentWidth = 260f;

    private RectTransform rect;
    private Canvas canvas;
    private VerticalLayoutGroup verticalLayout;
    private readonly TextMeshProUGUI[] layoutTexts = new TextMeshProUGUI[3];
    private ItemData displayedItem;
    private bool isCustomMode;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        verticalLayout = GetComponent<VerticalLayoutGroup>();

        layoutTexts[0] = nameText;
        layoutTexts[1] = descriptionText;
        layoutTexts[2] = extraText;

        EnsureStableLayoutSetup();
        gameObject.SetActive(false);
    }

    void Update()
    {
        FollowCursor();
        RefreshDynamicExtraIfNeeded();
    }

    void FollowCursor()
    {
        if (rect == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 pos
        );

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pivotFromBottomLeft = new Vector2(
            rect.rect.width * rect.pivot.x,
            rect.rect.height * rect.pivot.y
        );

        rect.anchoredPosition = pos + (offset / scale) + pivotFromBottomLeft;
    }

    public void Show(ItemData item)
    {
        isCustomMode = false;
        displayedItem = item;

        nameText.text = item.itemName;
        nameText.color = Color.white;

        descriptionText.text = item.description;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(item.description));

        extraText.text = BuildExtra(item);
        extraText.gameObject.SetActive(!string.IsNullOrEmpty(extraText.text));

        gameObject.SetActive(true);
        RebuildLayoutNow();
    }

    public void ShowCustom(string header, string description, string extra)
    {
        isCustomMode = true;
        displayedItem = null;

        nameText.text = string.IsNullOrEmpty(header) ? "Эффект" : header;
        nameText.color = Color.white;

        descriptionText.text = description ?? string.Empty;
        descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(descriptionText.text));

        extraText.text = extra ?? string.Empty;
        extraText.gameObject.SetActive(!string.IsNullOrEmpty(extraText.text));

        gameObject.SetActive(true);
        RebuildLayoutNow();
    }

    public void Hide()
    {
        displayedItem = null;
        isCustomMode = false;
        gameObject.SetActive(false);
    }

    string BuildExtra(ItemData item)
    {
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
                if (PlayerLantern.TryGetLanternCharge(item, out float currentCharge, out float maxCharge))
                    return "Заряд: " + Mathf.Max(0f, currentCharge).ToString("0") + "/" + Mathf.Max(1f, maxCharge).ToString("0");

                float defaultCharge = Mathf.Max(1f, item.lanternMaxCharge);
                return "Заряд: " + defaultCharge.ToString("0") + "/" + defaultCharge.ToString("0");
            case ItemType.Consumable:
                switch (item.consumableEffectType)
                {
                    case ConsumableEffectType.Heal:
                        return "Лечение: +" + Mathf.Max(0, item.healAmount) + "\n" +
                               "Перезарядка: " + Mathf.Max(0f, item.cooldown).ToString("0.#") + " сек";
                    case ConsumableEffectType.FogPressureProtection:
                        return "Рост давления x" + Mathf.Max(0f, item.pressureGrowthMultiplier).ToString("0.##") + "\n" +
                               "Длительность: " + Mathf.Max(0f, item.effectDuration).ToString("0.#") + " сек";
                    case ConsumableEffectType.FrostProtection:
                        return "Морозный урон x" + Mathf.Max(0f, item.frostDamageMultiplier).ToString("0.##") + "\n" +
                               "Длительность: " + Mathf.Max(0f, item.effectDuration).ToString("0.#") + " сек";
                    default:
                        return "Расходуемый предмет";
                }
            case ItemType.Equipment:
                float frostMul = Mathf.Max(0f, item.frostDamageMultiplier);
                float pressureMul = Mathf.Max(0f, item.pressureGrowthMultiplier);
                return "Экипировка\n" +
                       "Морозный урон x" + frostMul.ToString("0.##") + "\n" +
                       "Рост давления x" + pressureMul.ToString("0.##");
            default:
                return string.Empty;
        }
    }

    void RefreshDynamicExtraIfNeeded()
    {
        if (isCustomMode)
            return;

        if (!gameObject.activeInHierarchy || displayedItem == null)
            return;

        bool isLantern = displayedItem.type == ItemType.Lantern || displayedItem.isLantern;
        if (!isLantern)
            return;

        string updatedExtra = BuildExtra(displayedItem);
        if (extraText.text == updatedExtra)
            return;

        extraText.text = updatedExtra;
        extraText.gameObject.SetActive(!string.IsNullOrEmpty(updatedExtra));
        RebuildLayoutNow();
    }

    private void EnsureStableLayoutSetup()
    {
        if (verticalLayout != null)
            verticalLayout.enabled = false;

        var rootFitter = GetComponent<ContentSizeFitter>();
        if (rootFitter != null)
            rootFitter.enabled = false;

        ConfigureNameTextBlock(nameText);
        ConfigureTextBlock(descriptionText);
        ConfigureExtraTextBlock(extraText);
    }

    private static void ConfigureNameTextBlock(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

    private static void ConfigureTextBlock(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;

        var fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

   private static void ConfigureExtraTextBlock(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

    private void RebuildLayoutNow()
    {
        Canvas.ForceUpdateCanvases();

        ApplyManualLayout();

        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        Canvas.ForceUpdateCanvases();
    }

    private void ApplyManualLayout()
    {
        if (rect == null)
            return;

        float left = verticalLayout != null ? verticalLayout.padding.left : 8f;
        float right = verticalLayout != null ? verticalLayout.padding.right : 6f;
        float top = verticalLayout != null ? verticalLayout.padding.top : 8f;
        float bottom = verticalLayout != null ? verticalLayout.padding.bottom : 6f;
        float spacing = verticalLayout != null ? verticalLayout.spacing : 4f;

        float maxWidth = Mathf.Max(1f, maxContentWidth);
        float minWidthFromName = 1f;
        if (nameText != null && nameText.gameObject.activeSelf)
        {
            nameText.ForceMeshUpdate();
            minWidthFromName = Mathf.Max(1f, nameText.GetPreferredValues(nameText.text).x + 6f);
        }

        if (maxWidth < minWidthFromName)
            maxWidth = minWidthFromName;

        float contentWidth = minWidthFromName;
        int activeCount = 0;

        for (int i = 0; i < layoutTexts.Length; i++)
        {
            TextMeshProUGUI text = layoutTexts[i];
            if (text == null || !text.gameObject.activeSelf)
                continue;

            text.ForceMeshUpdate();
            Vector2 preferred = text.GetPreferredValues(text.text);
            contentWidth = Mathf.Max(contentWidth, Mathf.Min(preferred.x, maxWidth));
            activeCount++;
        }

        if (activeCount == 0)
        {
            rect.sizeDelta = new Vector2(left + right, top + bottom);
            return;
        }

        contentWidth = Mathf.Clamp(contentWidth, minWidthFromName, maxWidth);

        float y = top;
        for (int i = 0; i < layoutTexts.Length; i++)
        {
            TextMeshProUGUI text = layoutTexts[i];
            if (text == null || !text.gameObject.activeSelf)
                continue;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);

            Vector2 preferred = text.GetPreferredValues(text.text, contentWidth, 0f);
            float height = Mathf.Max(1f, preferred.y);

            textRect.sizeDelta = new Vector2(contentWidth, height);
            textRect.anchoredPosition = new Vector2(left, -y);

            y += height + spacing;
        }

        y -= spacing;

        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(left + contentWidth + right, y + bottom);
    }
}
