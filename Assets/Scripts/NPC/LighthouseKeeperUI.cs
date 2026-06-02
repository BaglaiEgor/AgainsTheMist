using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LighthouseKeeperUI : MonoBehaviour
{
    private enum KeeperPage
    {
        Main,
        Recipes,
        Items,
        Quest,
        Tips
    }

    [Header("Data")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private CraftingManager craftingManager;
    [SerializeField] private List<LighthouseKeeperQuest> quests = new();
    [SerializeField] private List<GuidanceEntry> tips = new();

    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ItemTooltip itemTooltip;
    [SerializeField] private CraftRecipeTooltipPresenter recipeTooltip;

    [Header("Window")]
    [SerializeField] private Canvas ownerCanvas;
    [SerializeField] private RectTransform rootPanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button recipesButton;
    [SerializeField] private Button itemsButton;
    [SerializeField] private Button questButton;
    [SerializeField] private Button tipsButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button[] backButtons;
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject recipesPage;
    [SerializeField] private GameObject itemsPage;
    [SerializeField] private GameObject questPage;
    [SerializeField] private GameObject tipsPage;

    [Header("Recipe")]
    [SerializeField] private RectTransform querySlotRect;
    [SerializeField] private Image queryIcon;
    [SerializeField] private TextMeshProUGUI queryText;
    [SerializeField] private RectTransform recipesContentRoot;
    [SerializeField] private GameObject recipeRowPrefab;
    [SerializeField] private Image dragIcon;

    [Header("Items")]
    [SerializeField] private RectTransform itemsContentRoot;
    [SerializeField] private GameObject itemRowPrefab;

    [Header("Quest")]
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI questRequirementText;
    [SerializeField] private TextMeshProUGUI questRewardText;
    [SerializeField] private Image questRequirementIcon;
    [SerializeField] private Image questRewardIcon;
    [SerializeField] private Button questSubmitButton;

    [Header("Tips")]
    [SerializeField] private TextMeshProUGUI tipTitleText;
    [SerializeField] private TextMeshProUGUI tipDescriptionText;
    [SerializeField] private Button nextTipButton;
    [SerializeField] private RectTransform unlockedTipsContentRoot;
    [SerializeField] private GameObject unlockedTipButtonPrefab;

    private LighthouseKeeperNPC currentKeeper;
    private KeeperPage currentPage;
    private ItemData recipeQueryItem;
    private ItemData draggedQueryItem;
    private int questIndex;
    private int selectedTipIndex = -1;
    private GuidanceEntry selectedUnlockedTip;

    private readonly List<LighthouseKeeperRecipeCell> spawnedRecipeRows = new();
    private readonly List<LighthouseKeeperRecipeCell> spawnedItemRows = new();
    private readonly List<LighthouseKeeperTipButton> spawnedTipRows = new();

    public bool IsOpen => rootPanel != null && rootPanel.gameObject.activeSelf;
    public bool IsRecipeQueryActive => IsOpen && currentPage == KeeperPage.Recipes;

    private void Awake()
    {
        HideDragIcon();
    }

    private void Update()
    {
        UpdateRecipeQueryDrag();
    }

    private void OnEnable()
    {
        GuidanceSystem.OnUnlockedGuidanceChanged += HandleUnlockedGuidanceChanged;
    }

    private void OnDisable()
    {
        GuidanceSystem.OnUnlockedGuidanceChanged -= HandleUnlockedGuidanceChanged;
    }

    private void OnDestroy()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshQuest;

        if (questSubmitButton != null)
            questSubmitButton.onClick.RemoveListener(TryCompleteQuest);

        if (nextTipButton != null)
            nextTipButton.onClick.RemoveListener(SelectRandomTip);
    }

    public void Initialize(Inventory inventory, InventoryUI owner)
    {
        playerInventory = inventory;
        inventoryUI = owner;

        if (ownerCanvas == null)
            ownerCanvas = GetComponentInParent<Canvas>();

        if (questSubmitButton != null)
        {
            questSubmitButton.onClick.RemoveListener(TryCompleteQuest);
            questSubmitButton.onClick.AddListener(TryCompleteQuest);
        }

        if (nextTipButton != null)
        {
            nextTipButton.onClick.RemoveListener(SelectRandomTip);
            nextTipButton.onClick.AddListener(SelectRandomTip);
            SetButtonText(nextTipButton, "Случайный совет");
        }

        BindNavigationButtons();

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshQuest;
            playerInventory.OnInventoryChanged += RefreshQuest;
        }

        SetupRecipeRows();
        SetupItemRows();
        SetupTips();
        ConfigureQuestRows();
        RefreshRecipeQuery();
        RefreshRecipes();
        RefreshItems();
        RefreshQuest();
    }

    private void BindNavigationButtons()
    {
        BindButton(recipesButton, ShowRecipes);
        BindButton(itemsButton, ShowItems);
        BindButton(questButton, ShowQuest);
        BindButton(tipsButton, ShowTips);
        BindButton(closeButton, Close);

        if (backButtons == null)
            return;

        for (int i = 0; i < backButtons.Length; i++)
            BindButton(backButtons[i], ShowMain);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void Open(LighthouseKeeperNPC keeper)
    {
        currentKeeper = keeper;
        SetVisible(true);
        ShowMain();
    }

    public void Close()
    {
        CancelRecipeQueryDrag();
        SetVisible(false);
        currentKeeper = null;
        itemTooltip?.Hide();
        recipeTooltip?.Hide();
    }

    public void ShowMain() => ShowPage(KeeperPage.Main);
    public void ShowRecipes() => ShowPage(KeeperPage.Recipes);
    public void ShowItems() => ShowPage(KeeperPage.Items);
    public void ShowQuest() => ShowPage(KeeperPage.Quest);
    public void ShowTips() => ShowPage(KeeperPage.Tips);

    public bool TrySetRecipeQuery(ItemData item)
    {
        if (!IsRecipeQueryActive || item == null)
            return false;

        SetRecipeQuery(item);
        return true;
    }

    public bool TryBeginRecipeQueryDrag(ItemData item)
    {
        if (!IsRecipeQueryActive || item == null || dragIcon == null)
            return false;

        draggedQueryItem = item;
        dragIcon.sprite = item.icon;
        dragIcon.enabled = item.icon != null;
        dragIcon.gameObject.SetActive(true);
        UpdateDragIconPosition();
        return true;
    }

    private void ShowPage(KeeperPage page)
    {
        currentPage = page;

        mainPage.SetActive(page == KeeperPage.Main);
        recipesPage.SetActive(page == KeeperPage.Recipes);
        itemsPage.SetActive(page == KeeperPage.Items);
        questPage.SetActive(page == KeeperPage.Quest);
        tipsPage.SetActive(page == KeeperPage.Tips);

        if (page == KeeperPage.Recipes)
        {
            RefreshRecipeQuery();
            RefreshRecipes();
        }
        else
        {
            CancelRecipeQueryDrag();
        }

        if (page == KeeperPage.Items)
            RefreshItems();

        if (page == KeeperPage.Quest)
            RefreshQuest();

        if (page == KeeperPage.Tips)
            RefreshTipsView();
    }

    private void SetRecipeQuery(ItemData item)
    {
        recipeQueryItem = item;
        RefreshRecipeQuery();
        RefreshRecipes();
    }

    private void RefreshRecipeQuery()
    {
        if (queryIcon != null)
        {
            queryIcon.sprite = recipeQueryItem != null ? recipeQueryItem.icon : null;
            queryIcon.enabled = queryIcon.sprite != null;
        }

        if (queryText != null)
            queryText.text = recipeQueryItem != null ? recipeQueryItem.itemName : "Перетащи предмет в ячейку";
    }

    private void RefreshRecipes()
    {
        int rowIndex = 0;
        IReadOnlyList<CraftingRecipe> recipes = craftingManager != null ? craftingManager.AllRecipes : null;

        if (recipes == null)
        {
            HideUnusedRecipeRows(0);
            return;
        }

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipe recipe = recipes[i];
            if (recipe == null || !RecipeMatchesQuery(recipe))
                continue;

            EnsureRecipeRow(rowIndex);
            if (rowIndex >= spawnedRecipeRows.Count)
                break;

            spawnedRecipeRows[rowIndex].Setup(recipe, craftingManager, recipeTooltip);
            rowIndex++;
        }

        HideUnusedRecipeRows(rowIndex);
    }

    private bool RecipeMatchesQuery(CraftingRecipe recipe)
    {
        if (recipeQueryItem == null)
            return false;

        if (recipe.result == recipeQueryItem)
            return true;

        if (recipe.ingredients == null)
            return false;

        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            if (recipe.ingredients[i].item == recipeQueryItem)
                return true;
        }

        return false;
    }

    private void RefreshItems()
    {
        int rowIndex = 0;
        IReadOnlyList<ItemData> items = itemDatabase != null ? itemDatabase.AllItems : null;

        if (items == null)
        {
            HideUnusedItemRows(0);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;

            EnsureItemRow(rowIndex);
            if (rowIndex >= spawnedItemRows.Count)
                break;

            spawnedItemRows[rowIndex].SetupItem(item, itemTooltip, SelectItemFromDatabase);
            rowIndex++;
        }

        HideUnusedItemRows(rowIndex);
    }

    private void SelectItemFromDatabase(ItemData item)
    {
        if (item == null)
            return;

        recipeQueryItem = item;
        ShowRecipes();
    }

    private void SetupTips()
    {
        selectedTipIndex = -1;
        selectedUnlockedTip = null;
        ClearSpawnedTipRows();
        RefreshTipsView();
    }

    private int FindNextTipIndex(int currentIndex)
    {
        if (tips == null || tips.Count == 0)
            return -1;

        int start = currentIndex < 0 ? 0 : (currentIndex + 1) % tips.Count;
        for (int step = 0; step < tips.Count; step++)
        {
            int index = (start + step) % tips.Count;
            if (tips[index] != null)
                return index;
        }

        return -1;
    }

    private void RefreshTipsView()
    {
        if (nextTipButton != null)
            nextTipButton.interactable = HasAnyTip();

        RefreshUnlockedTipRows();

        if (selectedUnlockedTip != null)
        {
            if (tipTitleText != null)
                tipTitleText.text = selectedUnlockedTip.Title;

            if (tipDescriptionText != null)
                tipDescriptionText.text = selectedUnlockedTip.Text;
            return;
        }

        GuidanceEntry tip = GetSelectedRandomTip();
        if (tip == null)
        {
            if (tipTitleText != null)
                tipTitleText.text = "Советы";

            if (tipDescriptionText != null)
                tipDescriptionText.text = HasUnlockedTips()
                    ? "Выбери найденную подсказку."
                    : "Нажми \"Случайный совет\" или вернись позже с найденной подсказкой.";
            return;
        }

        if (tipTitleText != null)
            tipTitleText.text = string.IsNullOrWhiteSpace(tip.Title) ? $"Совет {selectedTipIndex + 1}" : tip.Title;

        if (tipDescriptionText != null)
            tipDescriptionText.text = tip.Text;

        SelectTip(tip);
    }

    private void SelectRandomTip()
    {
        selectedUnlockedTip = null;
        selectedTipIndex = FindRandomTipIndex();
        RefreshTipsView();
    }

    private int FindRandomTipIndex()
    {
        if (tips == null || tips.Count == 0)
            return -1;

        List<int> validIndexes = new();
        for (int i = 0; i < tips.Count; i++)
        {
            if (tips[i] != null)
                validIndexes.Add(i);
        }

        if (validIndexes.Count == 0)
            return -1;

        if (validIndexes.Count == 1)
            return validIndexes[0];

        int index = validIndexes[Random.Range(0, validIndexes.Count)];
        if (index == selectedTipIndex)
            index = validIndexes[(validIndexes.IndexOf(index) + 1) % validIndexes.Count];

        return index;
    }

    private GuidanceEntry GetSelectedRandomTip()
    {
        if (tips == null || selectedTipIndex < 0 || selectedTipIndex >= tips.Count)
            return null;

        return tips[selectedTipIndex];
    }

    private bool HasAnyTip()
    {
        if (tips == null)
            return false;

        for (int i = 0; i < tips.Count; i++)
        {
            if (tips[i] != null)
                return true;
        }

        return false;
    }

    private void SelectUnlockedTip(GuidanceEntry tip)
    {
        selectedTipIndex = -1;
        selectedUnlockedTip = tip;
        RefreshTipsView();
        SelectTip(tip);
    }

    private void HandleUnlockedGuidanceChanged(IReadOnlyList<GuidanceEntry> _entries)
    {
        if (currentPage == KeeperPage.Tips)
            RefreshTipsView();
    }

    private void RefreshUnlockedTipRows()
    {
        IReadOnlyList<GuidanceEntry> entries = GuidanceSystem.Instance != null
            ? GuidanceSystem.Instance.UnlockedEntries
            : null;

        int count = entries != null ? entries.Count : 0;
        for (int i = 0; i < count; i++)
        {
            GuidanceEntry entry = entries[i];
            if (entry == null)
                continue;

            LighthouseKeeperTipButton row = EnsureTipRow(i);
            if (row != null)
                row.Setup(entry, SelectUnlockedTip);
        }

        for (int i = count; i < spawnedTipRows.Count; i++)
            spawnedTipRows[i].Setup(null, null);
    }

    private LighthouseKeeperTipButton EnsureTipRow(int rowIndex)
    {
        while (spawnedTipRows.Count <= rowIndex)
        {
            GameObject prefab = unlockedTipButtonPrefab != null ? unlockedTipButtonPrefab : recipeRowPrefab;
            GameObject rowObject = SpawnRowPrefab(unlockedTipsContentRoot, prefab, spawnedTipRows.Count);
            if (rowObject == null)
                return null;

            if (rowObject.GetComponent<Button>() == null)
                rowObject.AddComponent<Button>();

            LighthouseKeeperTipButton row = rowObject.GetComponent<LighthouseKeeperTipButton>();
            if (row == null)
                row = rowObject.AddComponent<LighthouseKeeperTipButton>();

            spawnedTipRows.Add(row);
        }

        return spawnedTipRows[rowIndex];
    }

    private void ClearSpawnedTipRows()
    {
        for (int i = 0; i < spawnedTipRows.Count; i++)
        {
            if (spawnedTipRows[i] != null)
                Destroy(spawnedTipRows[i].gameObject);
        }

        spawnedTipRows.Clear();
    }

    private bool HasUnlockedTips()
    {
        return GuidanceSystem.Instance != null && GuidanceSystem.Instance.UnlockedEntries.Count > 0;
    }

    private static void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = text;
    }

    private void SelectTip(GuidanceEntry tip)
    {
        if (tip != null && GuidanceSystem.Instance != null)
            GuidanceSystem.Instance.SetGuidance(tip);
    }

    private void RefreshQuest()
    {
        if (questTitleText == null || questSubmitButton == null)
            return;

        LighthouseKeeperQuest quest = GetCurrentQuest();
        if (quest == null)
        {
            questTitleText.text = "Все поручения выполнены";
            questDescriptionText.text = "Смотрителю пока больше нечего попросить.";
            questRequirementText.text = string.Empty;
            questRewardText.text = string.Empty;
            SetQuestIcon(questRequirementIcon, null);
            SetQuestIcon(questRewardIcon, null);
            questSubmitButton.interactable = false;
            return;
        }

        int current = CountInInventory(quest.RequiredItem);
        questTitleText.text = quest.Title;
        questDescriptionText.text = quest.Description;

        if (quest.RequiredItem != null)
            questRequirementText.text = $"{quest.RequiredItem.itemName} x{Mathf.Max(1, quest.RequiredAmount)}";
        else
            questRequirementText.text = string.Empty;

        SetQuestIcon(questRequirementIcon, quest.RequiredItem);

        ItemData rewardItem = ResolveRewardItem(quest);
        if (rewardItem != null)
            questRewardText.text = $"{rewardItem.itemName} x{Mathf.Max(1, quest.RewardAmount)}";
        else
            questRewardText.text = $"Награда x{Mathf.Max(1, quest.RewardAmount)}";

        SetQuestIcon(questRewardIcon, rewardItem);
        questSubmitButton.interactable = current >= quest.RequiredAmount;
    }

    private LighthouseKeeperQuest GetCurrentQuest()
    {
        if (questIndex < 0 || questIndex >= quests.Count)
            return null;

        return quests[questIndex];
    }

    private void TryCompleteQuest()
    {
        LighthouseKeeperQuest quest = GetCurrentQuest();
        if (quest == null || currentKeeper == null || playerInventory == null)
            return;

        if (quest.RequiredItem == null || quest.RewardPickupPrefab == null)
            return;

        if (!InventoryTransactionService.TryConsume(playerInventory, quest.RequiredItem, quest.RequiredAmount))
            return;

        if (!currentKeeper.TrySpawnQuestReward(quest))
            return;

        questIndex++;
        RefreshQuest();
    }

    private int CountInInventory(ItemData item)
    {
        if (playerInventory == null || item == null)
            return 0;

        int total = 0;
        for (int i = 0; i < playerInventory.SlotCount; i++)
        {
            InventoryItem slot = playerInventory.GetItem(i);
            if (slot != null && !slot.IsEmpty && slot.item == item)
                total += slot.amount;
        }

        return total;
    }

    private void UpdateRecipeQueryDrag()
    {
        TryPickItemFromRecipeQuerySlot();

        if (draggedQueryItem == null)
            return;

        UpdateDragIconPosition();

        if (Mouse.current == null || !Mouse.current.leftButton.wasReleasedThisFrame)
            return;

        if (IsPointerOverQuerySlot())
            SetRecipeQuery(draggedQueryItem);

        CancelRecipeQueryDrag();
    }

    private void TryPickItemFromRecipeQuerySlot()
    {
        if (!IsRecipeQueryActive || draggedQueryItem != null || recipeQueryItem == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (!IsPointerOverQuerySlot())
            return;

        draggedQueryItem = recipeQueryItem;
        recipeQueryItem = null;
        RefreshRecipeQuery();
        RefreshRecipes();

        if (dragIcon != null)
        {
            dragIcon.sprite = draggedQueryItem.icon;
            dragIcon.enabled = dragIcon.sprite != null;
            dragIcon.gameObject.SetActive(true);
            UpdateDragIconPosition();
        }
    }

    private bool IsPointerOverQuerySlot()
    {
        if (querySlotRect == null || Mouse.current == null)
            return false;

        Camera uiCamera = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? ownerCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(querySlotRect, Mouse.current.position.ReadValue(), uiCamera);
    }

    private void UpdateDragIconPosition()
    {
        if (dragIcon == null || ownerCanvas == null || Mouse.current == null)
            return;

        RectTransform canvasRect = ownerCanvas.transform as RectTransform;
        RectTransform iconRect = dragIcon.rectTransform;
        Camera uiCamera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Mouse.current.position.ReadValue(), uiCamera, out Vector2 localPoint);
        iconRect.anchoredPosition = localPoint;
    }

    private void CancelRecipeQueryDrag()
    {
        draggedQueryItem = null;
        HideDragIcon();
    }

    private void HideDragIcon()
    {
        if (dragIcon == null)
            return;

        dragIcon.sprite = null;
        dragIcon.enabled = false;
        dragIcon.gameObject.SetActive(false);
    }

    private void SetVisible(bool visible)
    {
        if (rootPanel == null)
            return;

        rootPanel.gameObject.SetActive(visible);

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    private void SetupRecipeRows()
    {
        spawnedRecipeRows.Clear();

        if (recipesContentRoot == null)
            return;

        for (int i = recipesContentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = recipesContentRoot.GetChild(i);
            LighthouseKeeperRecipeCell cell = child.GetComponent<LighthouseKeeperRecipeCell>();
            if (cell != null)
                Destroy(child.gameObject);
        }
    }

    private void EnsureRecipeRow(int rowIndex)
    {
        while (spawnedRecipeRows.Count <= rowIndex)
        {
            GameObject rowObject = SpawnRowPrefab(recipesContentRoot, recipeRowPrefab, spawnedRecipeRows.Count);
            if (rowObject == null)
                return;

            LighthouseKeeperRecipeCell cell = rowObject.GetComponent<LighthouseKeeperRecipeCell>();
            if (cell == null)
            {
                Destroy(rowObject);
                return;
            }

            spawnedRecipeRows.Add(cell);
        }
    }

    private void HideUnusedRecipeRows(int usedCount)
    {
        for (int i = usedCount; i < spawnedRecipeRows.Count; i++)
            spawnedRecipeRows[i].Setup(null, craftingManager, recipeTooltip);
    }

    private void SetupItemRows()
    {
        spawnedItemRows.Clear();

        if (itemsContentRoot == null)
            return;

        for (int i = itemsContentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = itemsContentRoot.GetChild(i);
            LighthouseKeeperRecipeCell cell = child.GetComponent<LighthouseKeeperRecipeCell>();
            if (cell != null)
                Destroy(child.gameObject);
        }
    }

    private void EnsureItemRow(int rowIndex)
    {
        while (spawnedItemRows.Count <= rowIndex)
        {
            GameObject rowObject = SpawnRowPrefab(itemsContentRoot, itemRowPrefab != null ? itemRowPrefab : recipeRowPrefab, spawnedItemRows.Count);
            if (rowObject == null)
                return;

            LighthouseKeeperRecipeCell cell = rowObject.GetComponent<LighthouseKeeperRecipeCell>();
            if (cell == null)
            {
                Destroy(rowObject);
                return;
            }

            spawnedItemRows.Add(cell);
        }
    }

    private void HideUnusedItemRows(int usedCount)
    {
        for (int i = usedCount; i < spawnedItemRows.Count; i++)
            spawnedItemRows[i].SetupItem(null, itemTooltip);
    }

    private static GameObject SpawnRowPrefab(RectTransform parent, GameObject prefab, int index)
    {
        if (parent == null || prefab == null)
            return null;

        GameObject rowObject = Object.Instantiate(prefab, parent);
        rowObject.name = $"Row_{index}";
        return rowObject;
    }

    private static void SetQuestIcon(Image iconTarget, ItemData item)
    {
        if (iconTarget == null)
            return;

        iconTarget.sprite = item != null ? item.icon : null;
        iconTarget.enabled = iconTarget.sprite != null;
    }

    private static ItemData ResolveRewardItem(LighthouseKeeperQuest quest)
    {
        if (quest == null || quest.RewardPickupPrefab == null)
            return null;

        PickupItem pickup = quest.RewardPickupPrefab.GetComponent<PickupItem>();
        return pickup != null ? pickup.itemData : null;
    }

    private void ConfigureQuestRows()
    {
        ApplyQuestTextMargin(questRequirementText);
        ApplyQuestTextMargin(questRewardText);
    }

    private static void ApplyQuestTextMargin(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        Vector4 margin = text.margin;
        margin.x = 30f;
        text.margin = margin;
    }
}
