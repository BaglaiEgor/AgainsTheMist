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
        return HandleFastTransfer(slotIndex);
    }

    public void HandleSlotLeftClick(int slotIndex)
    {
        if (inventoryUI == null || currentFurnace == null)
            return;

        inventoryUI.HandleSlotLeftClick(currentFurnace, slotIndex);
    }

    public void HandleSlotRightClick(int slotIndex)
    {
        if (inventoryUI == null || currentFurnace == null)
            return;

        inventoryUI.HandleSlotRightClick(currentFurnace, slotIndex);
    }

    public bool HandleFastTransfer(int slotIndex)
    {
        if (inventoryUI == null || currentFurnace == null)
            return false;

        return inventoryUI.HandleFastTransfer(currentFurnace, slotIndex);
    }

    public void SetHoveredSlot(int slotIndex)
    {
        if (inventoryUI == null || currentFurnace == null)
            return;

        inventoryUI.SetHoveredSlot(currentFurnace, slotIndex);
    }

    public void ClearHoveredSlot(int slotIndex)
    {
        if (inventoryUI == null || currentFurnace == null)
            return;

        inventoryUI.ClearHoveredSlot(currentFurnace, slotIndex);
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
            if (panelObject.activeSelf != visible || visible)
                UIPanelJuice.SetVisible(panelObject, visible);
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

