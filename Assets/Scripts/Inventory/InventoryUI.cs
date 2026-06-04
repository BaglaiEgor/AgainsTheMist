using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    private static readonly List<InventoryUI> Instances = new();

    [Header("Core")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private bool hideSlotParentParentInstead;
    [SerializeField] private Transform slotVisibilityRootOverride;
    [SerializeField] private Transform hotbarParent;
    [SerializeField] private Transform dragParent;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private EquipmentInventory equipmentInventory;

    [Header("Subcontrollers")]
    [SerializeField] private InventoryCursorInputController cursorInputController;
    [SerializeField] private InventoryWorldTooltipController worldTooltipController;

    [Header("Linked UI")]
    [SerializeField] private ItemTooltip tooltip;
    [SerializeField] private EquipmentSlotUI equipmentSlot;
    [SerializeField] private EquipmentSlotUI[] equipmentSlots;
    [SerializeField] private ChestInventoryUI chestInventoryUI;
    [SerializeField] private CraftingMenuUI craftingMenuUI;
    [SerializeField] private FurnaceInventoryUI furnaceInventoryUI;
    [SerializeField] private BeaconUpgradeUI beaconUpgradeUI;
    [SerializeField] private LighthouseKeeperUI keeperUI;

    [Header("Panels With Inventory")]
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject craftingPanel;

    private readonly List<InventorySlotUI> slots = new();
    private bool opened;
    private bool inventoryOpenedByModal;
    private bool stationCraftingModalOpen;
    private int hotbarSize;

    private ChestInventory currentOpenedChest;
    private CraftingStation currentOpenedStation;
    private FurnaceStation currentOpenedFurnace;
    private BeaconUpgrade currentOpenedBeacon;
    private LighthouseKeeperNPC currentOpenedKeeper;
    private LighthouseKeeperUI activeKeeperUI;

    public static bool HasAnyBlockingUiOpen
    {
        get
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                InventoryUI ui = Instances[i];
                if (ui == null)
                {
                    Instances.RemoveAt(i);
                    continue;
                }

                if (ui.ShouldBlockWorldInputFromUi())
                    return true;
            }

            return false;
        }
    }

    public Transform DragParent => dragParent;
    public Inventory PlayerInventory => inventory;
    public EquipmentInventory PlayerEquipment => equipmentInventory;
    public ChestInventory CurrentOpenedChest => currentOpenedChest;
    public FurnaceStation CurrentOpenedFurnace => currentOpenedFurnace;
    public bool IsInventoryOpened => opened;

    private void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    private void Start()
    {
        if (equipmentInventory == null && inventory != null)
            equipmentInventory = inventory.Equipment;

        BuildSlots();
        InitializeLinkedUi();

        if (inventory != null)
            inventory.OnInventoryChanged += Refresh;

        ChestInventory.OnAnyChestOpenStateChanged += HandleAnyChestOpenStateChanged;
        FurnaceStation.OnAnyFurnaceOpenStateChanged += HandleAnyFurnaceOpenStateChanged;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (cursorInputController != null)
            cursorInputController.Initialize(this, dragParent != null ? dragParent : transform);

        if (worldTooltipController != null)
            worldTooltipController.Initialize(canvas, playerController != null ? playerController.transform : null);

        Refresh();
        UpdateVisibility();
        UpdateMovementLockState();
    }

    private void Update()
    {
        HandleModalCloseHotkey();
        CloseModalUiIfOutOfRange();

        if (!opened && cursorInputController != null && cursorInputController.HasActiveIntent)
            cursorInputController.ResolveOnUiClose();

        cursorInputController?.Tick(playerController);
        worldTooltipController?.Tick();
    }

    private void OnDestroy()
    {
        Instances.Remove(this);

        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;

        ChestInventory.OnAnyChestOpenStateChanged -= HandleAnyChestOpenStateChanged;
        FurnaceStation.OnAnyFurnaceOpenStateChanged -= HandleAnyFurnaceOpenStateChanged;

        if (playerController != null)
            playerController.SetMovementLocked(false);
    }

    public void Toggle()
    {
        if (HasModalUiOpen())
        {
            CloseModalUi();
            return;
        }

        opened = !opened;
        if (!opened)
        {
            cursorInputController?.ResolveOnUiClose();
            HideAllTooltips();
        }

        UpdateVisibility();

        if (opened && craftingMenuUI != null)
            craftingMenuUI.Open(CraftStationType.None);

        UpdateMovementLockState();
    }

    public void OpenCrafting(CraftStationType stationType, CraftingStation station = null)
    {
        if (stationType == CraftStationType.None)
        {
            opened = true;
            UpdateVisibility();
            craftingMenuUI?.Open(CraftStationType.None);
            UpdateMovementLockState();
            return;
        }

        if (stationCraftingModalOpen && currentOpenedStation == station)
        {
            CloseStationModal();
            return;
        }

        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        CloseBeaconModal();
        CloseKeeperModal();
        EnsureInventoryOpenedForModal();

        stationCraftingModalOpen = true;
        currentOpenedStation = station;
        craftingMenuUI?.Open(stationType);
        UpdateMovementLockState();
    }

    public void OpenFurnace(FurnaceStation furnace)
    {
        if (furnace == null)
            return;

        if (currentOpenedFurnace == furnace && furnace.IsOpen)
        {
            furnace.Close();
            return;
        }

        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (stationCraftingModalOpen)
            CloseStationModal();

        if (currentOpenedFurnace != null && currentOpenedFurnace != furnace && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        CloseBeaconModal();
        CloseKeeperModal();
        EnsureInventoryOpenedForModal();

        currentOpenedFurnace = furnace;
        furnace.Open();
        UpdateMovementLockState();
    }

    public void OpenBeacon(BeaconUpgrade beaconUpgrade)
    {
        if (beaconUpgrade == null || beaconUpgradeUI == null)
            return;

        if (currentOpenedBeacon == beaconUpgrade)
        {
            CloseBeaconModal();
            if (!HasModalUiOpen())
                CloseModalInventoryIfNeeded();

            UpdateMovementLockState();
            return;
        }

        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        if (stationCraftingModalOpen)
            CloseStationModal();

        CloseBeaconModal();
        CloseKeeperModal();
        EnsureInventoryOpenedForModal();

        currentOpenedBeacon = beaconUpgrade;
        beaconUpgradeUI.ShowBeacon(beaconUpgrade);
        UpdateMovementLockState();
    }

    public void OpenKeeper(LighthouseKeeperNPC keeper)
    {
        LighthouseKeeperUI targetKeeperUi = ResolveKeeperUi(keeper);
        if (keeper == null || targetKeeperUi == null)
            return;

        if (currentOpenedKeeper == keeper && targetKeeperUi.IsOpen)
        {
            CloseKeeperModal();
            if (!HasModalUiOpen())
                CloseModalInventoryIfNeeded();

            UpdateMovementLockState();
            return;
        }

        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        if (stationCraftingModalOpen)
            CloseStationModal();

        CloseBeaconModal();
        EnsureInventoryOpenedForModal();

        currentOpenedKeeper = keeper;
        activeKeeperUI = targetKeeperUi;
        activeKeeperUI.Initialize(inventory, this);
        activeKeeperUI.Open(keeper);
        UpdateMovementLockState();
    }

    public void HandleSlotLeftClick(IItemContainer container, int slotIndex)
    {
        cursorInputController?.HandleSlotLeftClick(container, slotIndex);
    }

    public void HandleSlotRightClick(IItemContainer container, int slotIndex)
    {
        cursorInputController?.HandleSlotRightClick(container, slotIndex);
    }

    public bool HandleFastTransfer(IItemContainer container, int slotIndex)
    {
        if (TrySetKeeperRecipeQueryInstant(container, slotIndex))
            return true;

        return ItemRoutingService.TryFastTransfer(new ItemSlotRef(container, slotIndex), this);
    }

    public bool TrySetKeeperRecipeQueryInstant(IItemContainer container, int slotIndex)
    {
        LighthouseKeeperUI targetKeeperUi = GetActiveKeeperUi();
        if (targetKeeperUi == null || !targetKeeperUi.IsRecipeQueryActive || !ReferenceEquals(container, inventory))
            return false;

        InventoryItem slot = inventory.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item == null)
            return false;

        return targetKeeperUi.TrySetRecipeQuery(slot.item);
    }

    public bool TryStartKeeperRecipeQueryDrag(IItemContainer container, int slotIndex)
    {
        LighthouseKeeperUI targetKeeperUi = GetActiveKeeperUi();
        if (targetKeeperUi == null || !targetKeeperUi.IsRecipeQueryActive || !ReferenceEquals(container, inventory))
            return false;

        InventoryItem slot = inventory.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item == null)
            return false;

        return targetKeeperUi.TryBeginRecipeQueryDrag(slot.item);
    }

    public bool TrySetKeeperRecipeQueryFromCursor()
    {
        LighthouseKeeperUI targetKeeperUi = GetActiveKeeperUi();
        if (targetKeeperUi == null || !targetKeeperUi.IsRecipeQueryActive)
            return false;

        if (!TryGetCursorStack(out ItemData item, out _) || item == null)
            return false;

        if (!targetKeeperUi.TrySetRecipeQuery(item))
            return false;

        cursorInputController?.ResolveOnUiClose();
        NotifySlotVisualRefresh();
        return true;
    }

    public void SetHoveredSlot(IItemContainer container, int slotIndex)
    {
        cursorInputController?.SetHoveredSlot(container, slotIndex);
    }

    public void ClearHoveredSlot(IItemContainer container, int slotIndex)
    {
        cursorInputController?.ClearHoveredSlot(container, slotIndex);
    }

    public bool IsCursorSourceSlot(IItemContainer container, int slotIndex)
    {
        return cursorInputController != null && cursorInputController.IsSourceSlot(container, slotIndex);
    }

    public bool TryGetCursorStack(out ItemData item, out int amount)
    {
        item = null;
        amount = 0;
        return cursorInputController != null && cursorInputController.TryGetCursorStack(out item, out amount);
    }

    public bool TryConsumeCursorItem(int amount)
    {
        return cursorInputController != null && cursorInputController.TryConsumeCursorItem(amount);
    }

    public void ShowTooltip(ItemData item)
    {
        if (tooltip != null && item != null)
            tooltip.Show(item);
    }

    public void HideTooltip()
    {
        tooltip?.Hide();
    }

    private void HideAllTooltips()
    {
        tooltip?.Hide();
        craftingMenuUI?.HideTooltip();
        chestInventoryUI?.HideTooltip();
        furnaceInventoryUI?.HideTooltip();
    }

    public void NotifySlotVisualRefresh()
    {
        Refresh();
    }

    private void BuildSlots()
    {
        if (inventory == null || slotPrefab == null || slotParent == null)
            return;

        hotbarSize = Mathf.Clamp(inventory.HotbarSize, 1, inventory.SlotCount);
        slots.Clear();

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            Transform targetParent = i < hotbarSize && hotbarParent != null ? hotbarParent : slotParent;
            GameObject obj = Instantiate(slotPrefab, targetParent);
            InventorySlotUI slot = obj.GetComponent<InventorySlotUI>();
            if (slot == null)
                continue;

            slot.Init(inventory, this, i, dragParent);
            slots.Add(slot);
        }
    }

    private void InitializeLinkedUi()
    {
        if (equipmentSlots != null && equipmentSlots.Length > 0)
        {
            for (int i = 0; i < equipmentSlots.Length; i++)
                equipmentSlots[i]?.Init(equipmentInventory, this);
        }
        else
        {
            equipmentSlot?.Init(equipmentInventory, this);
        }

        chestInventoryUI?.Initialize(inventory, tooltip, slotPrefab, this);
        furnaceInventoryUI?.Initialize(inventory, tooltip, slotPrefab, this);
        beaconUpgradeUI?.Initialize(inventory, this);
        keeperUI?.Initialize(inventory, this);
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].Refresh();

        if (inventory != null)
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetHighlight(i == inventory.ActiveSlotIndex);
        }

        if (equipmentSlots != null && equipmentSlots.Length > 0)
        {
            for (int i = 0; i < equipmentSlots.Length; i++)
                equipmentSlots[i]?.Refresh();
        }
        else
        {
            equipmentSlot?.Refresh();
        }
    }

    private void UpdateVisibility()
    {
        Transform slotVisibilityRoot = ResolveSlotVisibilityRoot();
        if (slotVisibilityRoot != null)
            UIPanelJuice.SetVisible(slotVisibilityRoot.gameObject, opened);

        for (int i = hotbarSize; i < slots.Count; i++)
            slots[i].gameObject.SetActive(opened);

        if (equipmentPanel != null)
            UIPanelJuice.SetVisible(equipmentPanel, opened);

        if (craftingPanel != null)
            UIPanelJuice.SetVisible(craftingPanel, opened);
    }

    private Transform ResolveSlotVisibilityRoot()
    {
        if (slotVisibilityRootOverride != null)
            return slotVisibilityRootOverride;

        if (slotParent == null)
            return null;

        if (!hideSlotParentParentInstead)
            return slotParent;

        return slotParent.parent != null ? slotParent.parent : slotParent;
    }

    private void HandleModalCloseHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (HasModalUiOpen())
            CloseModalUi();
    }

    private void HandleAnyChestOpenStateChanged(ChestInventory chest, bool openedState)
    {
        if (chest == null)
            return;

        if (openedState)
        {
            if (stationCraftingModalOpen)
                CloseStationModal();

            if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
                currentOpenedFurnace.Close();

            CloseBeaconModal();
            CloseKeeperModal();
            currentOpenedChest = chest;
            EnsureInventoryOpenedForModal();
        }
        else if (chest == currentOpenedChest)
        {
            currentOpenedChest = null;
            if (!HasModalUiOpen())
                CloseModalInventoryIfNeeded();
        }

        UpdateMovementLockState();
    }

    private void HandleAnyFurnaceOpenStateChanged(FurnaceStation furnace, bool openedState)
    {
        if (furnace == null)
            return;

        if (openedState)
        {
            if (stationCraftingModalOpen)
                CloseStationModal();

            if (currentOpenedChest != null && currentOpenedChest.IsOpen)
                currentOpenedChest.Close();

            CloseBeaconModal();
            CloseKeeperModal();
            currentOpenedFurnace = furnace;
            EnsureInventoryOpenedForModal();
        }
        else if (furnace == currentOpenedFurnace)
        {
            currentOpenedFurnace = null;
            if (!HasModalUiOpen())
                CloseModalInventoryIfNeeded();
        }

        UpdateMovementLockState();
    }

    private bool ShouldBlockWorldInputFromUi()
    {
        return HasModalUiOpen();
    }

    private void UpdateMovementLockState()
    {
        if (playerController == null)
            return;

        playerController.SetMovementLocked(false);
    }

    private void CloseModalUiIfOutOfRange()
    {
        Transform playerTransform = playerController != null ? playerController.transform : null;
        if (playerTransform == null || !HasModalUiOpen())
            return;

        if (currentOpenedChest != null && !currentOpenedChest.CanInteract(playerTransform))
        {
            CloseModalUi();
            return;
        }

        if (stationCraftingModalOpen && currentOpenedStation != null && !currentOpenedStation.CanInteract(playerTransform))
        {
            CloseModalUi();
            return;
        }

        if (currentOpenedFurnace != null)
        {
            CraftingStation furnaceStation = currentOpenedFurnace.GetComponent<CraftingStation>();
            if (furnaceStation != null && !furnaceStation.CanInteract(playerTransform))
            {
                CloseModalUi();
                return;
            }
        }

        if (currentOpenedBeacon != null && !currentOpenedBeacon.CanInteract(playerTransform))
        {
            CloseModalUi();
            return;
        }

        if (currentOpenedKeeper != null && !currentOpenedKeeper.CanInteract(playerTransform))
            CloseModalUi();
    }

    private bool HasModalUiOpen()
    {
        return stationCraftingModalOpen ||
               currentOpenedChest != null ||
               currentOpenedFurnace != null ||
               currentOpenedBeacon != null ||
               currentOpenedKeeper != null;
    }

    private void EnsureInventoryOpenedForModal()
    {
        if (opened)
            return;

        opened = true;
        inventoryOpenedByModal = true;
        UpdateVisibility();
    }

    private void CloseModalInventoryIfNeeded()
    {
        if (!inventoryOpenedByModal)
            return;

        opened = false;
        inventoryOpenedByModal = false;
        HideAllTooltips();
        UpdateVisibility();
    }

    private void CloseModalUi()
    {
        cursorInputController?.ResolveOnUiClose();
        HideAllTooltips();

        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        currentOpenedChest = null;
        currentOpenedFurnace = null;
        CloseBeaconModal();
        CloseKeeperModal();

        if (stationCraftingModalOpen)
            CloseStationModal();

        CloseModalInventoryIfNeeded();
        UpdateMovementLockState();
    }

    private void CloseStationModal()
    {
        cursorInputController?.ResolveOnUiClose();
        HideAllTooltips();
        stationCraftingModalOpen = false;
        currentOpenedStation = null;
        craftingMenuUI?.Close();

        if (!HasModalUiOpen())
            CloseModalInventoryIfNeeded();

        UpdateMovementLockState();
    }

    private void CloseBeaconModal()
    {
        cursorInputController?.ResolveOnUiClose();
        HideAllTooltips();
        beaconUpgradeUI?.HideBeacon();
        currentOpenedBeacon = null;
    }

    private void CloseKeeperModal()
    {
        cursorInputController?.ResolveOnUiClose();
        HideAllTooltips();
        activeKeeperUI?.Close();
        if (activeKeeperUI == null)
            keeperUI?.Close();

        activeKeeperUI = null;
        currentOpenedKeeper = null;
    }

    private LighthouseKeeperUI ResolveKeeperUi(LighthouseKeeperNPC keeper)
    {
        if (keeper != null && keeper.KeeperUI != null)
            return keeper.KeeperUI;

        return keeperUI;
    }

    private LighthouseKeeperUI GetActiveKeeperUi()
    {
        if (activeKeeperUI != null)
            return activeKeeperUI;

        return keeperUI;
    }
}
