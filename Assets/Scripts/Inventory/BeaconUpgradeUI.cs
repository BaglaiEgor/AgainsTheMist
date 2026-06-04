using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BeaconUpgradeUI : MonoBehaviour
{
    [System.Serializable]
    private class RequirementRowView
    {
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI text;
    }

    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private RectTransform rootPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI radiusText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RectTransform requirementsRoot;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Rows")]
    [SerializeField, Min(1)] private int requirementRowCount = 5;
    [SerializeField] private List<RequirementRowView> requirementRows = new();

    [Header("Visual")]
    [SerializeField] private Color enoughColor = new Color(0.45f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color missingColor = new Color(0.95f, 0.4f, 0.4f, 1f);

    private BeaconUpgrade currentBeacon;
    private bool initialized;

    public BeaconUpgrade CurrentBeacon => currentBeacon;

    private void Awake()
    {
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;

        UnbindBeacon();
        UnbindButton();
    }

    public void Initialize(Inventory inventory, InventoryUI owner)
    {
        if (inventory != null)
            playerInventory = inventory;

        if (owner != null)
            inventoryUI = owner;

        BindButton();
        initialized = ValidateReferences(true);

        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= HandleInventoryChanged;

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += HandleInventoryChanged;

        SetPanelVisible(false);
        Refresh();
    }

    public void ShowBeacon(BeaconUpgrade beacon)
    {
        if (beacon == null)
            return;

        if (!initialized)
            initialized = ValidateReferences(false);

        if (!initialized)
            return;

        if (currentBeacon != beacon)
        {
            UnbindBeacon();
            currentBeacon = beacon;
            currentBeacon.OnBeaconUpgradeStateChanged += Refresh;
        }

        SetPanelVisible(true);
        Refresh();
    }

    public void HideBeacon()
    {
        UnbindBeacon();
        SetPanelVisible(false);
    }

    private void HandleInventoryChanged()
    {
        if (!IsVisible())
            return;

        Refresh();
    }

    private void TryUpgradeCurrentBeacon()
    {
        if (currentBeacon == null || playerInventory == null)
            return;

        currentBeacon.TryUpgrade(playerInventory);
        Refresh();
    }

    private void Refresh()
    {
        if (titleText != null)
            titleText.text = "Маяк";

        if (upgradeButtonText != null)
            upgradeButtonText.text = "Улучшить";

        if (currentBeacon == null)
        {
            if (levelText != null)
                levelText.text = "Текущий уровень: -";

            if (radiusText != null)
                radiusText.text = "Радиус: -";

            if (statusText != null)
                statusText.text = "Выберите маяк";

            SetUpgradeInteractable(false);
            HideAllRequirementRows();
            return;
        }

        int currentLevel = currentBeacon.CurrentLevel;
        int maxLevel = Mathf.Max(0, currentBeacon.MaxLevel);
        if (levelText != null)
            levelText.text = $"Текущий уровень: {currentLevel}/{maxLevel}";

        if (radiusText != null)
            radiusText.text = $"Радиус: {currentBeacon.CurrentRadius:0.##}";

        if (currentBeacon.IsMaxLevel)
        {
            if (statusText != null)
                statusText.text = "Максимальный уровень";

            SetUpgradeInteractable(false);
            HideAllRequirementRows();
            return;
        }

        if (!currentBeacon.TryGetNextLevel(out BeaconUpgrade.UpgradeLevel nextLevel) || nextLevel == null)
        {
            if (statusText != null)
                statusText.text = "Данные улучшения не найдены";

            SetUpgradeInteractable(false);
            HideAllRequirementRows();
            return;
        }

        if (statusText != null)
            statusText.text = $"Следующий бонус: +{Mathf.Max(0.01f, nextLevel.radiusIncrease):0.##} к радиусу";

        bool hasEnough = PopulateRequirementRows(nextLevel);
        SetUpgradeInteractable(hasEnough);
    }

    private bool PopulateRequirementRows(BeaconUpgrade.UpgradeLevel nextLevel)
    {
        if (nextLevel.requiredResources == null || nextLevel.requiredResources.Length == 0)
        {
            HideAllRequirementRows();
            return true;
        }

        bool hasEnough = true;
        int rowIndex = 0;

        for (int i = 0; i < nextLevel.requiredResources.Length; i++)
        {
            BeaconUpgrade.RequiredResource requirement = nextLevel.requiredResources[i];
            if (requirement == null || requirement.item == null)
                continue;

            if (rowIndex >= requirementRows.Count)
                break;

            RequirementRowView row = requirementRows[rowIndex];
            if (row == null || row.root == null)
            {
                rowIndex++;
                continue;
            }

            int need = Mathf.Max(1, requirement.amount);
            int current = CountInInventory(requirement.item);
            bool enough = current >= need;
            hasEnough &= enough;

            row.root.gameObject.SetActive(true);
            if (row.icon != null)
            {
                row.icon.sprite = requirement.item.icon;
                row.icon.enabled = row.icon.sprite != null;
            }

            if (row.text != null)
            {
                row.text.text = $"{requirement.item.itemName} {current}/{need}";
                row.text.color = enough ? enoughColor : missingColor;
                EnsurePixelOutline(row.text);
            }

            rowIndex++;
        }

        for (int i = rowIndex; i < requirementRows.Count; i++)
        {
            if (requirementRows[i] != null && requirementRows[i].root != null)
                requirementRows[i].root.gameObject.SetActive(false);
        }

        return hasEnough;
    }

    private void HideAllRequirementRows()
    {
        for (int i = 0; i < requirementRows.Count; i++)
        {
            RequirementRowView row = requirementRows[i];
            if (row != null && row.root != null)
                row.root.gameObject.SetActive(false);
        }
    }

    private void EnsurePixelOutline(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (!Application.isPlaying)
            return;
    }

    private int CountInInventory(ItemData item)
    {
        if (playerInventory == null || item == null)
            return 0;

        int total = 0;
        for (int i = 0; i < playerInventory.SlotCount; i++)
        {
            InventoryItem slot = playerInventory.GetItem(i);
            if (slot == null || slot.IsEmpty || slot.item != item)
                continue;

            total += slot.amount;
        }

        return total;
    }

    private void SetUpgradeInteractable(bool interactable)
    {
        if (upgradeButton != null)
            upgradeButton.interactable = interactable;
    }

    private void UnbindBeacon()
    {
        if (currentBeacon != null)
            currentBeacon.OnBeaconUpgradeStateChanged -= Refresh;

        currentBeacon = null;
    }

    private void BindButton()
    {
        if (upgradeButton == null)
            return;

        upgradeButton.onClick.RemoveListener(TryUpgradeCurrentBeacon);
        upgradeButton.onClick.AddListener(TryUpgradeCurrentBeacon);
    }

    private void UnbindButton()
    {
        if (upgradeButton == null)
            return;

        upgradeButton.onClick.RemoveListener(TryUpgradeCurrentBeacon);
    }

    private bool ValidateReferences(bool log)
    {
        bool valid = true;

        if (playerInventory == null)
        {
            if (log) Debug.LogWarning("BeaconUpgradeUI: playerInventory is not assigned.");
            valid = false;
        }

        if (rootPanel == null)
        {
            if (log) Debug.LogWarning("BeaconUpgradeUI: rootPanel is not assigned.");
            valid = false;
        }

        if (levelText == null || radiusText == null || statusText == null)
        {
            if (log) Debug.LogWarning("BeaconUpgradeUI: assign level/radius/status texts.");
            valid = false;
        }

        if (upgradeButton == null)
        {
            if (log) Debug.LogWarning("BeaconUpgradeUI: upgradeButton is not assigned.");
            valid = false;
        }

        if (requirementsRoot == null)
        {
            if (log) Debug.LogWarning("BeaconUpgradeUI: requirementsRoot is not assigned.");
            valid = false;
        }

        return valid;
    }

    private bool IsVisible()
    {
        if (rootPanel == null)
            return false;

        if (rootPanel.gameObject != gameObject)
            return rootPanel.gameObject.activeSelf;

        return panelCanvasGroup != null && panelCanvasGroup.alpha > 0.001f;
    }

    private void SetPanelVisible(bool visible)
    {
        if (rootPanel == null)
            return;

        if (rootPanel.gameObject != gameObject)
        {
            if (rootPanel.gameObject.activeSelf != visible || visible)
                UIPanelJuice.SetVisible(rootPanel.gameObject, visible);
            return;
        }

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        EditorEnsureSceneUi();
    }

    private void EditorEnsureSceneUi()
    {
        if (rootPanel == null)
            rootPanel = transform as RectTransform;

        if (rootPanel == null)
            return;

        if (rootPanel.gameObject == gameObject && panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (titleText == null)
            titleText = EnsureText("Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-20f, 24f), 18f, TextAlignmentOptions.Center, Color.white);

        if (levelText == null)
            levelText = EnsureText("LevelText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(-20f, 22f), 14f, TextAlignmentOptions.Left, new Color(0.9f, 0.9f, 0.9f, 1f));

        if (radiusText == null)
            radiusText = EnsureText("RadiusText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(-20f, 22f), 14f, TextAlignmentOptions.Left, new Color(0.9f, 0.9f, 0.9f, 1f));

        if (statusText == null)
            statusText = EnsureText("StatusText", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(-20f, 22f), 13f, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.85f, 1f));

        if (requirementsRoot == null)
            requirementsRoot = EnsureContainer("Requirements", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(-20f, 96f));

        EnsureRequirementRows();

        if (upgradeButton == null)
            upgradeButton = EnsureButton();

        if (upgradeButton != null)
        {
            if (upgradeButtonText == null)
            {
                TextMeshProUGUI childText = upgradeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (childText != null)
                    upgradeButtonText = childText;
            }

            if (upgradeButtonText != null)
                upgradeButtonText.text = "Улучшить";
        }

        if (titleText != null)
            titleText.text = "Маяк";
    }

    private RectTransform EnsureContainer(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        Transform existing = rootPanel.Find(name);
        RectTransform rect;
        if (existing != null)
        {
            rect = existing as RectTransform;
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(rootPanel, false);
            rect = go.GetComponent<RectTransform>();
        }

        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        return rect;
    }

    private TextMeshProUGUI EnsureText(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        Transform existing = rootPanel.Find(name);
        TextMeshProUGUI text;

        if (existing != null)
            text = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(rootPanel, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        return text;
    }

    private void EnsureRequirementRows()
    {
        if (requirementsRoot == null)
            return;

        requirementRowCount = Mathf.Max(1, requirementRowCount);
        while (requirementRows.Count < requirementRowCount)
            requirementRows.Add(new RequirementRowView());

        for (int i = 0; i < requirementRows.Count; i++)
        {
            RequirementRowView row = requirementRows[i];
            string rowName = $"RequirementRow_{i + 1}";
            Transform existing = requirementsRoot.Find(rowName);
            if (row.root == null)
                row.root = existing as RectTransform;

            if (row.root == null)
            {
                GameObject rowObject = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                rowObject.transform.SetParent(requirementsRoot, false);
                row.root = rowObject.GetComponent<RectTransform>();
            }

            row.root.anchorMin = new Vector2(0f, 1f);
            row.root.anchorMax = new Vector2(1f, 1f);
            row.root.pivot = new Vector2(0.5f, 1f);
            row.root.anchoredPosition = new Vector2(0f, -i * 20f);
            row.root.sizeDelta = new Vector2(0f, 18f);

            HorizontalLayoutGroup layout = row.root.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = row.root.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            if (row.icon == null)
            {
                Transform iconTransform = row.root.Find("Icon");
                if (iconTransform != null)
                    row.icon = iconTransform.GetComponent<Image>();
            }

            if (row.icon == null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(row.root, false);
                row.icon = iconObject.GetComponent<Image>();

                LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
                iconLayout.minWidth = 16f;
                iconLayout.minHeight = 16f;
                iconLayout.preferredWidth = 16f;
                iconLayout.preferredHeight = 16f;
            }

            if (row.text == null)
            {
                Transform textTransform = row.root.Find("Text");
                if (textTransform != null)
                    row.text = textTransform.GetComponent<TextMeshProUGUI>();
            }

            if (row.text == null)
            {
                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                textObject.transform.SetParent(row.root, false);
                row.text = textObject.GetComponent<TextMeshProUGUI>();

                LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
                textLayout.flexibleWidth = 1f;
            }

            if (row.text != null)
            {
                row.text.fontSize = 12f;
                row.text.alignment = TextAlignmentOptions.Left;
                row.text.raycastTarget = false;
                EnsurePixelOutline(row.text);
            }
        }
    }

    private Button EnsureButton()
    {
        Transform existing = rootPanel.Find("UpgradeButton");
        Button button;

        if (existing != null)
        {
            button = existing.GetComponent<Button>();
        }
        else
        {
            GameObject buttonObject = new GameObject("UpgradeButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(rootPanel, false);
            button = buttonObject.GetComponent<Button>();
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-10f, 10f);
        buttonRect.sizeDelta = new Vector2(130f, 32f);

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.2f, 0.5f, 0.25f, 0.95f);

        if (upgradeButtonText == null)
        {
            Transform textTransform = button.transform.Find("Text");
            if (textTransform != null)
                upgradeButtonText = textTransform.GetComponent<TextMeshProUGUI>();
        }

        if (upgradeButtonText == null)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(button.transform, false);
            upgradeButtonText = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform textRect = upgradeButtonText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        upgradeButtonText.fontSize = 14f;
        upgradeButtonText.alignment = TextAlignmentOptions.Center;
        upgradeButtonText.color = Color.white;
        upgradeButtonText.raycastTarget = false;
        upgradeButtonText.text = "Улучшить";

        return button;
    }
#endif
}
