using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChestInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ItemTooltip tooltip;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private RectTransform rootPanel;
    [SerializeField] private RectTransform slotsRoot;
    [SerializeField] private TextMeshProUGUI titleText;

    private readonly List<ChestSlotUI> slotViews = new List<ChestSlotUI>();
    private ChestInventory currentChest;
    private bool initialized;

    public ChestInventory CurrentChest => currentChest;

    void Awake()
    {
        if (rootPanel != null)
            rootPanel.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        ChestInventory.OnAnyChestOpenStateChanged += HandleAnyChestOpenStateChanged;
    }

    void OnDisable()
    {
        ChestInventory.OnAnyChestOpenStateChanged -= HandleAnyChestOpenStateChanged;
        HideTooltip();
    }

    void Update()
    {
        if (currentChest == null || rootPanel == null)
            return;

        if (!rootPanel.gameObject.activeSelf && currentChest.IsOpen)
            currentChest.Close();
    }

    public void Initialize(Inventory inventory, ItemTooltip sharedTooltip, GameObject sharedSlotPrefab, InventoryUI sharedInventoryUi = null)
    {
        if (inventory != null)
            playerInventory = inventory;

        if (sharedInventoryUi != null)
            inventoryUI = sharedInventoryUi;

        if (sharedTooltip != null)
            tooltip = sharedTooltip;

        if (sharedSlotPrefab != null)
            slotPrefab = sharedSlotPrefab;

        initialized = ValidateRequiredReferences(true);
        if (!initialized)
            return;

        if (rootPanel != null)
            rootPanel.gameObject.SetActive(false);
    }

    public void TryTakeFromSlot(int index)
    {
        if (currentChest == null || playerInventory == null)
            return;

        InventoryItem slotItem = currentChest.GetItem(index);
        if (slotItem == null || slotItem.IsEmpty || slotItem.item == null || slotItem.amount <= 0)
            return;

        if (!playerInventory.CanAddItem(slotItem.item, slotItem.amount))
        {
            Debug.Log("Inventory full, item remains in chest");
            return;
        }

        if (!currentChest.TryExtractStack(index, out ItemData item, out int amount))
            return;

        playerInventory.Add(item, amount);
    }

    public bool TryTransferFromChestSlot(int index, bool wholeStack)
    {
        if (inventoryUI == null)
            return false;

        return inventoryUI.TryTransferFromChestSlot(index, wholeStack);
    }

    public bool TryDropChestSlotToPlayerSlot(int chestSlotIndex, int playerSlotIndex)
    {
        if (inventoryUI == null)
            return false;

        return inventoryUI.TryDropChestSlotToPlayerSlot(chestSlotIndex, playerSlotIndex);
    }

    public bool TrySwapChestSlots(int sourceIndex, int targetIndex)
    {
        if (currentChest == null)
            return false;

        currentChest.Swap(sourceIndex, targetIndex);
        return true;
    }

    public Transform GetDragParent()
    {
        if (inventoryUI == null)
            return null;

        return inventoryUI.DragParent;
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

    void HandleAnyChestOpenStateChanged(ChestInventory chest, bool opened)
    {
        if (!initialized)
            initialized = ValidateRequiredReferences(false);

        if (!initialized)
            return;

        if (opened)
        {
            ShowChest(chest);
            return;
        }

        if (chest == currentChest)
            HideChest();
    }

    void ShowChest(ChestInventory chest)
    {
        if (chest == null || rootPanel == null)
            return;

        if (currentChest != chest)
        {
            UnbindCurrentChest();
            currentChest = chest;
            currentChest.OnChestChanged += Refresh;
        }

        rootPanel.gameObject.SetActive(true);
        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(chest.name) ? "Сундук" : chest.name;

        EnsureSlotViews();
        Refresh();
    }

    void HideChest()
    {
        UnbindCurrentChest();
        if (rootPanel != null)
            rootPanel.gameObject.SetActive(false);
    }

    void UnbindCurrentChest()
    {
        if (currentChest != null)
            currentChest.OnChestChanged -= Refresh;

        currentChest = null;
        HideTooltip();
    }

    void Refresh()
    {
        if (currentChest == null)
            return;

        EnsureSlotViews();

        for (int i = 0; i < slotViews.Count; i++)
        {
            InventoryItem item = i < currentChest.SlotCount ? currentChest.GetItem(i) : null;
            slotViews[i].Refresh(item);
            slotViews[i].gameObject.SetActive(i < currentChest.SlotCount);
        }
    }

    bool ValidateRequiredReferences(bool log)
    {
        bool valid = true;

        if (playerInventory == null)
        {
            if (log)
                Debug.LogWarning("ChestInventoryUI: playerInventory is not assigned.");
            valid = false;
        }

        if (inventoryUI == null)
        {
            if (log)
                Debug.LogWarning("ChestInventoryUI: inventoryUI is not assigned.");
            valid = false;
        }

        if (slotPrefab == null)
        {
            if (log)
                Debug.LogWarning("ChestInventoryUI: slotPrefab is not assigned.");
            valid = false;
        }

        if (rootPanel == null)
        {
            if (log)
                Debug.LogWarning("ChestInventoryUI: rootPanel is not assigned.");
            valid = false;
        }

        if (slotsRoot == null)
        {
            if (log)
                Debug.LogWarning("ChestInventoryUI: slotsRoot is not assigned.");
            valid = false;
        }

        return valid;
    }

    void EnsureSlotViews()
    {
        if (slotsRoot == null || slotPrefab == null || currentChest == null)
            return;

        int targetCount = Mathf.Max(1, currentChest.SlotCount);
        while (slotViews.Count < targetCount)
        {
            GameObject slotObject = Instantiate(slotPrefab, slotsRoot);

            InventorySlotUI inventorySlotUi = slotObject.GetComponent<InventorySlotUI>();
            if (inventorySlotUi != null)
            {
                inventorySlotUi.enabled = false;
                Destroy(inventorySlotUi);
            }

            ChestSlotUI chestSlotUi = slotObject.GetComponent<ChestSlotUI>();
            if (chestSlotUi == null)
                chestSlotUi = slotObject.AddComponent<ChestSlotUI>();

            int slotIndex = slotViews.Count;
            chestSlotUi.Init(this, slotIndex);
            slotViews.Add(chestSlotUi);
        }
    }

}
