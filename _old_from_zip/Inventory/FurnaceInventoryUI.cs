using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FurnaceInventoryUI : MonoBehaviour
{
    private const string DefaultTitle = "Печь";

    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ItemTooltip tooltip;
    [SerializeField] private RectTransform rootPanel;
    [SerializeField] private RectTransform fuelSlotRoot;
    [SerializeField] private RectTransform inputSlotRoot;
    [SerializeField] private RectTransform outputSlotRoot;

    [Header("Slot Views")]
    [SerializeField] private FurnaceSlotUI fuelSlotView;
    [SerializeField] private FurnaceSlotUI inputSlotView;
    [SerializeField] private FurnaceSlotUI outputSlotView;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI flowHintText;
    [SerializeField] private TextMeshProUGUI fuelStatusText;
    [SerializeField] private TextMeshProUGUI smeltStatusText;

    [Header("Fuel Bar")]
    [SerializeField] private Image fuelStatusFill;
    [SerializeField] private Image smeltStatusFill;

    [Header("Visibility")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private FurnaceStation currentFurnace;
    private bool initialized;
    private bool warnedMissingCanvasGroup;

    public FurnaceStation CurrentFurnace => currentFurnace;

    private void Awake()
    {
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
        FurnaceStation.OnAnyFurnaceOpenStateChanged += HandleAnyFurnaceOpenStateChanged;
    }

    private void OnDisable()
    {
        FurnaceStation.OnAnyFurnaceOpenStateChanged -= HandleAnyFurnaceOpenStateChanged;
        HideTooltip();
    }

    public void Initialize(Inventory inventory, ItemTooltip sharedTooltip, GameObject _unusedSlotPrefab, InventoryUI sharedInventoryUi = null)
    {
        if (inventory != null)
            playerInventory = inventory;

        if (sharedInventoryUi != null)
            inventoryUI = sharedInventoryUi;

        if (sharedTooltip != null)
            tooltip = sharedTooltip;

        BindSlotViews();
        ApplyStaticTexts();

        initialized = ValidateRequiredReferences(true);
        if (!initialized)
            return;

        SetPanelVisible(false);
        RefreshFuelStatus();
    }

    public void ShowFurnace(FurnaceStation furnace)
    {
        if (furnace == null || rootPanel == null)
            return;

        if (!initialized)
            initialized = ValidateRequiredReferences(false);

        if (!initialized)
            return;

        if (currentFurnace != furnace)
        {
            UnbindCurrentFurnace();
            currentFurnace = furnace;
            currentFurnace.OnFurnaceChanged += Refresh;
        }

        if (titleText != null)
            titleText.text = DefaultTitle;

        SetPanelVisible(true);
        Refresh();
    }

    public void HideFurnace()
    {
        UnbindCurrentFurnace();
        SetPanelVisible(false);
    }

    public bool TryTransferFromFurnaceSlot(int slotIndex, bool wholeStack)
    {
        if (currentFurnace == null || playerInventory == null)
            return false;

        FurnaceSlotType slotType = ToSlotType(slotIndex);
        InventoryItem sourceSlot = currentFurnace.GetSlot(slotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0)
            return false;

        int requestAmount = wholeStack ? sourceSlot.amount : 1;
        if (!playerInventory.CanAddItem(sourceSlot.item, requestAmount))
            return false;

        if (!currentFurnace.TryRemoveFromSlot(slotType, requestAmount, out ItemData removedItem, out int removedAmount))
            return false;

        if (removedItem == null || removedAmount <= 0)
            return false;

        playerInventory.Add(removedItem, removedAmount);
        return true;
    }

    public bool TryDropFurnaceSlotToPlayerSlot(int furnaceSlotIndex, int playerSlotIndex)
    {
        if (currentFurnace == null || playerInventory == null)
            return false;

        FurnaceSlotType sourceType = ToSlotType(furnaceSlotIndex);
        InventoryItem sourceSlot = currentFurnace.GetSlot(sourceType);
        InventoryItem targetSlot = playerInventory.GetItem(playerSlotIndex);

        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0 || targetSlot == null)
            return false;

        ItemData sourceItem = sourceSlot.item;
        int sourceAmount = sourceSlot.amount;
        ItemData targetItem = targetSlot.item;
        int targetAmount = targetSlot.amount;

        if (targetSlot.IsEmpty)
        {
            if (!playerInventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount))
                return false;

            currentFurnace.TrySetSlot(sourceType, null, 0);
            return true;
        }

        if (targetItem == sourceItem)
        {
            int maxStack = Mathf.Max(1, sourceItem.maxStack);
            int freeSpace = Mathf.Max(0, maxStack - targetAmount);
            if (freeSpace <= 0)
                return false;

            int moveAmount = Mathf.Min(freeSpace, sourceAmount);
            if (!playerInventory.TryAddToSlot(playerSlotIndex, sourceItem, moveAmount, out int addedAmount) || addedAmount <= 0)
                return false;

            currentFurnace.TryRemoveFromSlot(sourceType, addedAmount, out _, out _);
            return true;
        }

        if (sourceType == FurnaceSlotType.Output)
            return false;

        if (!currentFurnace.TrySetSlot(sourceType, targetItem, targetAmount))
            return false;

        if (playerInventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount))
            return true;

        currentFurnace.TrySetSlot(sourceType, sourceItem, sourceAmount);
        return false;
    }

    public bool TrySwapFurnaceSlots(int sourceIndex, int targetIndex)
    {
        if (currentFurnace == null || sourceIndex == targetIndex)
            return false;

        FurnaceSlotType sourceType = ToSlotType(sourceIndex);
        FurnaceSlotType targetType = ToSlotType(targetIndex);

        InventoryItem source = currentFurnace.GetSlot(sourceType);
        InventoryItem target = currentFurnace.GetSlot(targetType);
        if (source == null || target == null)
            return false;

        ItemData sourceItem = source.item;
        int sourceAmount = source.amount;
        ItemData targetItem = target.item;
        int targetAmount = target.amount;

        if (sourceItem != null && !currentFurnace.CanAddToSlot(targetType, sourceItem, sourceAmount))
            return false;

        if (targetItem != null && !currentFurnace.CanAddToSlot(sourceType, targetItem, targetAmount))
            return false;

        if (!currentFurnace.TrySetSlot(sourceType, targetItem, targetAmount))
            return false;

        if (currentFurnace.TrySetSlot(targetType, sourceItem, sourceAmount))
            return true;

        currentFurnace.TrySetSlot(sourceType, sourceItem, sourceAmount);
        return false;
    }

    public void ShowTooltip(ItemData item)
    {
        if (tooltip == null || item == null)
            return;

        tooltip.Show(item);
    }

    public void HideTooltip()
    {
        if (tooltip == null)
            return;

        tooltip.Hide();
    }

    public Transform GetDragParent()
    {
        return inventoryUI != null ? inventoryUI.DragParent : null;
    }

    private void HandleAnyFurnaceOpenStateChanged(FurnaceStation furnace, bool opened)
    {
        if (!initialized)
            initialized = ValidateRequiredReferences(false);

        if (!initialized)
            return;

        if (opened)
            ShowFurnace(furnace);
        else if (furnace == currentFurnace)
            HideFurnace();
    }

    private void Refresh()
    {
        RefreshSlotView(fuelSlotView, FurnaceSlotType.Fuel);
        RefreshSlotView(inputSlotView, FurnaceSlotType.Input);
        RefreshSlotView(outputSlotView, FurnaceSlotType.Output);
        RefreshFuelStatus();
        RefreshSmeltStatus();
    }

    private void RefreshSlotView(FurnaceSlotUI slotView, FurnaceSlotType slotType)
    {
        if (slotView == null)
            return;

        InventoryItem item = currentFurnace != null ? currentFurnace.GetSlot(slotType) : null;
        slotView.Refresh(item);
    }

    private void BindSlotViews()
    {
        if (fuelSlotView == null && fuelSlotRoot != null)
            fuelSlotView = fuelSlotRoot.GetComponentInChildren<FurnaceSlotUI>(true);

        if (inputSlotView == null && inputSlotRoot != null)
            inputSlotView = inputSlotRoot.GetComponentInChildren<FurnaceSlotUI>(true);

        if (outputSlotView == null && outputSlotRoot != null)
            outputSlotView = outputSlotRoot.GetComponentInChildren<FurnaceSlotUI>(true);

        if (fuelSlotView != null)
            fuelSlotView.Init(this, 0);

        if (inputSlotView != null)
            inputSlotView.Init(this, 1);

        if (outputSlotView != null)
            outputSlotView.Init(this, 2);
    }

    private void UnbindCurrentFurnace()
    {
        if (currentFurnace != null)
            currentFurnace.OnFurnaceChanged -= Refresh;

        currentFurnace = null;
        HideTooltip();
    }

    private bool ValidateRequiredReferences(bool log)
    {
        BindSlotViews();
        ApplyStaticTexts();

        bool valid = true;

        if (playerInventory == null)
        {
            if (log)
                Debug.LogWarning("FurnaceInventoryUI: playerInventory is not assigned.");
            valid = false;
        }

        if (inventoryUI == null)
        {
            if (log)
                Debug.LogWarning("FurnaceInventoryUI: inventoryUI is not assigned.");
            valid = false;
        }

        if (rootPanel == null)
        {
            if (log)
                Debug.LogWarning("FurnaceInventoryUI: rootPanel is not assigned.");
            valid = false;
        }

        if (fuelSlotView == null || inputSlotView == null || outputSlotView == null)
        {
            if (log)
                Debug.LogWarning("FurnaceInventoryUI: assign FurnaceSlotUI for fuel/input/output on scene.");
            valid = false;
        }

        if (fuelStatusFill == null || fuelStatusText == null || smeltStatusFill == null || smeltStatusText == null)
        {
            if (log)
                Debug.LogWarning("FurnaceInventoryUI: status bars UI is not fully assigned.");
            valid = false;
        }

        return valid;
    }

    private void ApplyStaticTexts()
    {
        if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            titleText.text = DefaultTitle;

        if (flowHintText != null && string.IsNullOrWhiteSpace(flowHintText.text))
            flowHintText.text = "Топливо + руда -> переплавка";
    }

    private void SetPanelVisible(bool visible)
    {
        if (rootPanel == null)
            return;

        GameObject panelObject = rootPanel.gameObject;
        if (panelObject != gameObject)
        {
            if (panelObject.activeSelf != visible)
                panelObject.SetActive(visible);
            return;
        }

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
        {
            if (!warnedMissingCanvasGroup)
            {
                warnedMissingCanvasGroup = true;
                Debug.LogWarning("FurnaceInventoryUI: rootPanel is on same object. Add CanvasGroup to control visibility.");
            }

            return;
        }

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    private void RefreshFuelStatus()
    {
        if (fuelStatusText == null || fuelStatusFill == null)
            return;

        if (currentFurnace == null)
        {
            fuelStatusFill.fillAmount = 0f;
            fuelStatusText.text = "Топливо: 0%";
            return;
        }

        float fuelTotal = Mathf.Max(0f, currentFurnace.FuelTimeTotal);
        float fuelRemaining = Mathf.Max(0f, currentFurnace.FuelTimeRemaining);

        if (fuelRemaining > 0f && fuelTotal > 0f)
        {
            float percent = Mathf.Clamp01(fuelRemaining / fuelTotal);
            fuelStatusFill.fillAmount = percent;
            fuelStatusText.text = $"Топливо: {percent * 100f:0}% ({fuelRemaining:0.0} c)";
            return;
        }

        InventoryItem fuelSlot = currentFurnace.GetSlot(FurnaceSlotType.Fuel);
        if (fuelSlot != null && !fuelSlot.IsEmpty && fuelSlot.item != null && fuelSlot.amount > 0)
        {
            fuelStatusFill.fillAmount = 0f;
            fuelStatusText.text = $"Топливо: 0% (готово: {fuelSlot.amount} шт.)";
            return;
        }

        fuelStatusFill.fillAmount = 0f;
        fuelStatusText.text = "Топливо: 0% (пусто)";
    }

    private void RefreshSmeltStatus()
    {
        if (smeltStatusFill == null || smeltStatusText == null)
            return;

        if (currentFurnace == null)
        {
            smeltStatusFill.fillAmount = 0f;
            smeltStatusText.text = "Переплавка: 0%";
            return;
        }

        InventoryItem inputSlot = currentFurnace.GetSlot(FurnaceSlotType.Input);
        bool hasInput = inputSlot != null && !inputSlot.IsEmpty && inputSlot.item != null && inputSlot.amount > 0;
        if (!hasInput)
        {
            smeltStatusFill.fillAmount = 0f;
            smeltStatusText.text = "Переплавка: 0% (нет руды)";
            return;
        }

        float smeltDuration = Mathf.Max(0.1f, currentFurnace.CurrentSmeltDuration);
        float progress = Mathf.Clamp01(currentFurnace.SmeltProgress / smeltDuration);
        smeltStatusFill.fillAmount = progress;
        smeltStatusText.text = $"Переплавка: {progress * 100f:0}%";
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
            return;

        EnsurePanelCanvasGroup();
        EnsureTitle();
        EnsureFlowHint();

        EnsureSlotRoot(ref fuelSlotRoot, "FuelSlotRoot", new Vector2(-112f, -8f));
        EnsureSlotRoot(ref inputSlotRoot, "InputSlotRoot", new Vector2(0f, -8f));
        EnsureSlotRoot(ref outputSlotRoot, "OutputSlotRoot", new Vector2(112f, -8f));

        EnsureSlotView(ref fuelSlotView, fuelSlotRoot, "Топливо", 0);
        EnsureSlotView(ref inputSlotView, inputSlotRoot, "Руда", 1);
        EnsureSlotView(ref outputSlotView, outputSlotRoot, "Результат", 2);

        EnsureFuelStatusBars();
        ApplyStaticTexts();
    }

    private void EnsurePanelCanvasGroup()
    {
        if (rootPanel.gameObject != gameObject)
            return;

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void EnsureTitle()
    {
        if (titleText == null)
        {
            Transform title = rootPanel.Find("Title");
            if (title != null)
                titleText = title.GetComponent<TextMeshProUGUI>();
        }

        if (titleText == null)
            titleText = CreateText(rootPanel, "Title", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-20f, 26f), 18f, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.82f, 1f));

        titleText.text = DefaultTitle;
    }

    private void EnsureFlowHint()
    {
        if (flowHintText == null)
        {
            Transform hint = rootPanel.Find("FlowHint");
            if (hint != null)
                flowHintText = hint.GetComponent<TextMeshProUGUI>();
        }

        if (flowHintText == null)
            flowHintText = CreateText(rootPanel, "FlowHint", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(-20f, 18f), 12f, TextAlignmentOptions.Center, new Color(0.85f, 0.85f, 0.85f, 0.95f));

        flowHintText.text = "Топливо + руда -> переплавка";
    }

    private void EnsureSlotRoot(ref RectTransform slotRoot, string rootName, Vector2 anchoredPosition)
    {
        if (slotRoot != null)
            return;

        Transform existing = rootPanel.Find(rootName);
        if (existing != null)
            slotRoot = existing as RectTransform;

        if (slotRoot != null)
            return;

        GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
        rootObject.transform.SetParent(rootPanel, false);
        slotRoot = rootObject.GetComponent<RectTransform>();
        slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
        slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
        slotRoot.pivot = new Vector2(0.5f, 0.5f);
        slotRoot.anchoredPosition = anchoredPosition;
        slotRoot.sizeDelta = new Vector2(64f, 64f);
    }

    private void EnsureSlotView(ref FurnaceSlotUI slotView, RectTransform slotRoot, string labelText, int index)
    {
        if (slotRoot == null)
            return;

        EnsureSlotLabel(slotRoot, labelText);

        if (slotView == null)
            slotView = slotRoot.GetComponentInChildren<FurnaceSlotUI>(true);

        if (slotView == null)
        {
            GameObject slotObject = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(FurnaceSlotUI));
            slotObject.transform.SetParent(slotRoot, false);

            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = Vector2.zero;
            slotRect.sizeDelta = new Vector2(64f, 64f);

            Image background = slotObject.GetComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(slotObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.offsetMin = new Vector2(8f, 8f);
            iconRect.offsetMax = new Vector2(-8f, -8f);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;

            TextMeshProUGUI amount = CreateText(slotRect, "Amount", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(44f, 18f), 14f, TextAlignmentOptions.BottomRight, Color.white);
            amount.raycastTarget = false;

            GameObject highlightObject = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            highlightObject.transform.SetParent(slotObject.transform, false);
            RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0f, 0f);
            highlightRect.anchorMax = new Vector2(1f, 1f);
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;
            Image highlightImage = highlightObject.GetComponent<Image>();
            highlightImage.color = new Color(1f, 0.9f, 0.2f, 0.2f);
            highlightImage.enabled = false;

            slotView = slotObject.GetComponent<FurnaceSlotUI>();
        }

        slotView.Init(this, index);
    }

    private void EnsureSlotLabel(RectTransform slotRoot, string label)
    {
        Transform labelTransform = slotRoot.Find("Label");
        TextMeshProUGUI labelText;

        if (labelTransform == null)
        {
            labelText = CreateText(slotRoot, "Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(110f, 20f), 12f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.9f));
        }
        else
        {
            labelText = labelTransform.GetComponent<TextMeshProUGUI>();
        }

        if (labelText != null)
            labelText.text = label;
    }

    private void EnsureFuelStatusBars()
    {
        Transform statusTransform = rootPanel.Find("FuelStatus");
        RectTransform statusRoot;

        if (statusTransform == null)
        {
            GameObject statusObject = new GameObject("FuelStatus", typeof(RectTransform), typeof(Image));
            statusObject.transform.SetParent(rootPanel, false);
            statusRoot = statusObject.GetComponent<RectTransform>();
        }
        else
        {
            statusRoot = statusTransform as RectTransform;
        }

        if (statusRoot == null)
            return;

        statusRoot.anchorMin = new Vector2(0f, 0f);
        statusRoot.anchorMax = new Vector2(1f, 0f);
        statusRoot.pivot = new Vector2(0.5f, 0f);
        statusRoot.anchoredPosition = new Vector2(0f, 8f);
        statusRoot.sizeDelta = new Vector2(-20f, 20f);

        Image background = statusRoot.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.75f);

        Transform fillTransform = statusRoot.Find("Fill");
        if (fillTransform == null)
        {
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(statusRoot, false);
            fillTransform = fillObject.transform;
        }

        fuelStatusFill = fillTransform.GetComponent<Image>();
        if (fuelStatusFill == null)
            fuelStatusFill = fillTransform.gameObject.AddComponent<Image>();

        RectTransform fillRect = fuelStatusFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        fuelStatusFill.type = Image.Type.Filled;
        fuelStatusFill.fillMethod = Image.FillMethod.Horizontal;
        fuelStatusFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fuelStatusFill.color = new Color(0.96f, 0.52f, 0.16f, 0.95f);

        Transform labelTransform = statusRoot.Find("Label");
        if (labelTransform == null)
            fuelStatusText = CreateText(statusRoot, "Label", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, 0f), 12f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.95f));
        else
            fuelStatusText = labelTransform.GetComponent<TextMeshProUGUI>();

        Transform smeltTransform = rootPanel.Find("SmeltStatus");
        RectTransform smeltRoot;

        if (smeltTransform == null)
        {
            GameObject smeltObject = new GameObject("SmeltStatus", typeof(RectTransform), typeof(Image));
            smeltObject.transform.SetParent(rootPanel, false);
            smeltRoot = smeltObject.GetComponent<RectTransform>();
        }
        else
        {
            smeltRoot = smeltTransform as RectTransform;
        }

        if (smeltRoot == null)
            return;

        smeltRoot.anchorMin = new Vector2(0f, 0f);
        smeltRoot.anchorMax = new Vector2(1f, 0f);
        smeltRoot.pivot = new Vector2(0.5f, 0f);
        smeltRoot.anchoredPosition = new Vector2(0f, 30f);
        smeltRoot.sizeDelta = new Vector2(-20f, 20f);

        Image smeltBackground = smeltRoot.GetComponent<Image>();
        if (smeltBackground != null)
            smeltBackground.color = new Color(0.04f, 0.04f, 0.04f, 0.75f);

        Transform smeltFillTransform = smeltRoot.Find("Fill");
        if (smeltFillTransform == null)
        {
            GameObject smeltFillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            smeltFillObject.transform.SetParent(smeltRoot, false);
            smeltFillTransform = smeltFillObject.transform;
        }

        smeltStatusFill = smeltFillTransform.GetComponent<Image>();
        if (smeltStatusFill == null)
            smeltStatusFill = smeltFillTransform.gameObject.AddComponent<Image>();

        RectTransform smeltFillRect = smeltStatusFill.rectTransform;
        smeltFillRect.anchorMin = new Vector2(0f, 0f);
        smeltFillRect.anchorMax = new Vector2(1f, 1f);
        smeltFillRect.offsetMin = new Vector2(2f, 2f);
        smeltFillRect.offsetMax = new Vector2(-2f, -2f);

        smeltStatusFill.type = Image.Type.Filled;
        smeltStatusFill.fillMethod = Image.FillMethod.Horizontal;
        smeltStatusFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        smeltStatusFill.color = new Color(0.31f, 0.78f, 0.96f, 0.95f);

        Transform smeltLabelTransform = smeltRoot.Find("Label");
        if (smeltLabelTransform == null)
            smeltStatusText = CreateText(smeltRoot, "Label", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, 0f), 12f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.95f));
        else
            smeltStatusText = smeltLabelTransform.GetComponent<TextMeshProUGUI>();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
#endif

    private static FurnaceSlotType ToSlotType(int index)
    {
        return index switch
        {
            0 => FurnaceSlotType.Fuel,
            1 => FurnaceSlotType.Input,
            _ => FurnaceSlotType.Output,
        };
    }
}
