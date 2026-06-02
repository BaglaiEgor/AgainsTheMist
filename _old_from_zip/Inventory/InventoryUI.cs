using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private Transform hotbarParent;

    [SerializeField] private Transform dragParent;

    private readonly List<InventorySlotUI> slots = new();
    private readonly List<RaycastResult> uiRaycastResults = new();
    private bool opened;
    private bool stationCraftingModalOpen;
    private bool inventoryOpenedByModal;
    private ChestInventory currentOpenedChest;
    private CraftingStation currentOpenedStation;
    private FurnaceStation currentOpenedFurnace;
    private BeaconUpgrade currentOpenedBeacon;
    private int hotbarSize;

    [SerializeField] private ItemTooltip tooltip;
    [SerializeField] private EquipmentSlotUI equipmentSlot;
    [SerializeField] private ChestInventoryUI chestInventoryUI;
    [SerializeField] private CraftingMenuUI craftingMenuUI;
    [SerializeField] private FurnaceInventoryUI furnaceInventoryUI;
    [SerializeField] private BeaconUpgradeUI beaconUpgradeUI;

    [Header("Panels With Inventory")]
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject craftingPanel;

    [Header("World Interact Tooltip")]
    [SerializeField] private GameObject worldInteractTooltipRoot;
    [SerializeField] private RectTransform worldInteractTooltipRect;
    [SerializeField] private Image worldInteractTooltipIcon;
    [SerializeField] private TextMeshProUGUI worldInteractTooltipText;
    [SerializeField] private string worldInteractLabel = "\u041E\u0442\u043A\u0440\u044B\u0442\u044C";
    [SerializeField] private Vector2 worldInteractTooltipOffset = new Vector2(20f, 20f);
    [SerializeField] private Transform interactionSource;
    [SerializeField] private bool hideWorldTooltipOverUI = true;
    [SerializeField] private PlayerController playerController;

    private Canvas canvas;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        ResolvePlayerController();

        if (beaconUpgradeUI == null)
            beaconUpgradeUI = GetComponentInChildren<BeaconUpgradeUI>(true);

        hotbarSize = Mathf.Clamp(inventory.HotbarSize, 1, inventory.SlotCount);

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            Transform targetParent = i < hotbarSize && hotbarParent != null
                ? hotbarParent
                : slotParent;

            var obj = Instantiate(slotPrefab, targetParent);
            var slot = obj.GetComponent<InventorySlotUI>();
            slot.Init(inventory, this, i, dragParent);
            slots.Add(slot);
        }

        if (equipmentSlot != null)
            equipmentSlot.Init(inventory, this);
        else
            Debug.LogWarning("InventoryUI: equipmentSlot is not assigned in Inspector.");

        if (chestInventoryUI != null)
            chestInventoryUI.Initialize(inventory, tooltip, slotPrefab, this);
        else
            Debug.LogWarning("InventoryUI: chestInventoryUI is not assigned in Inspector.");

        if (furnaceInventoryUI != null)
            furnaceInventoryUI.Initialize(inventory, tooltip, slotPrefab, this);
        else
            Debug.LogWarning("InventoryUI: furnaceInventoryUI is not assigned in Inspector.");

        if (beaconUpgradeUI != null)
            beaconUpgradeUI.Initialize(inventory, this);
        else
            Debug.LogWarning("InventoryUI: beaconUpgradeUI is not assigned in Inspector.");

        inventory.OnInventoryChanged += Refresh;
        ChestInventory.OnAnyChestOpenStateChanged += HandleAnyChestOpenStateChanged;
        FurnaceStation.OnAnyFurnaceOpenStateChanged += HandleAnyFurnaceOpenStateChanged;
        Refresh();
        UpdateVisibility();
        UpdateMovementLockState();
        HideWorldInteractTooltip();
    }

    void Update()
    {
        HandleModalCloseHotkey();
        UpdateWorldInteractTooltip();
    }

    void OnDestroy()
    {
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
            if (craftingMenuUI != null)
                craftingMenuUI.Open(CraftStationType.None);
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

        EnsureInventoryOpenedForModal();
        stationCraftingModalOpen = true;
        currentOpenedStation = station;

        if (craftingMenuUI != null)
            craftingMenuUI.Open(stationType);

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
        EnsureInventoryOpenedForModal();

        currentOpenedBeacon = beaconUpgrade;
        beaconUpgradeUI.ShowBeacon(beaconUpgrade);
        UpdateMovementLockState();
    }

    void UpdateVisibility()
    {
        for (int i = hotbarSize; i < slots.Count; i++)
            slots[i].gameObject.SetActive(opened);

        if (equipmentPanel != null)
            equipmentPanel.SetActive(opened);

        if (craftingPanel != null)
            craftingPanel.SetActive(opened);
    }

    void Refresh()
    {
        foreach (var slot in slots)
            slot.Refresh();

        for (int i = 0; i < slots.Count; i++)
            slots[i].SetHighlight(i == inventory.ActiveSlotIndex);

        if (equipmentSlot != null)
            equipmentSlot.Refresh();

        Debug.Log("Inventory UI refreshed");
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

    private void UpdateWorldInteractTooltip()
    {
        if (worldInteractTooltipRoot == null)
            return;

        if (hideWorldTooltipOverUI && IsPointerOverBlockingUI())
        {
            HideWorldInteractTooltip();
            return;
        }

        if (Mouse.current == null || Camera.main == null)
        {
            HideWorldInteractTooltip();
            return;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (!TryGetHoveredInteractable(out _))
        {
            HideWorldInteractTooltip();
            return;
        }

        ShowWorldInteractTooltip(screenPos);
    }

    private bool TryGetHoveredInteractable(out Component interactable)
    {
        interactable = null;

        if (Camera.main == null || Mouse.current == null)
            return false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        if (hits == null || hits.Length == 0)
            return false;

        Transform source = ResolveInteractionSource();
        Component stationCandidate = null;
        BeaconUpgrade beaconCandidate = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            ChestInventory chest = hit.GetComponentInParent<ChestInventory>();
            if (chest != null)
            {
                if (source != null && !chest.CanInteract(source))
                    continue;

                interactable = chest;
                return true;
            }

            if (stationCandidate != null)
            {
                // We still want to discover beacon if no station was found.
                if (beaconCandidate != null)
                    continue;
            }

            CraftingStation station = hit.GetComponentInParent<CraftingStation>();
            if (station == null)
                continue;

            if (source != null && !station.CanInteract(source))
                continue;

            if (station.StationType == CraftStationType.Furnace)
                stationCandidate = ResolveFurnaceForStation(station);
            else
                stationCandidate = station;

            continue;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            BeaconUpgrade beaconUpgrade = hit.GetComponentInParent<BeaconUpgrade>();
            if (beaconUpgrade == null)
                continue;

            beaconCandidate = beaconUpgrade;
            break;
        }

        if (stationCandidate != null)
        {
            interactable = stationCandidate;
            return true;
        }

        if (beaconCandidate != null)
        {
            interactable = beaconCandidate;
            return true;
        }

        return false;
    }

    private Transform ResolveInteractionSource()
    {
        if (interactionSource != null)
            return interactionSource;

        PlayerController playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
            interactionSource = playerController.transform;

        return interactionSource;
    }

    private void ShowWorldInteractTooltip(Vector2 screenPosition)
    {
        if (worldInteractTooltipText != null)
            worldInteractTooltipText.text = worldInteractLabel;

        if (worldInteractTooltipIcon != null)
            worldInteractTooltipIcon.enabled = worldInteractTooltipIcon.sprite != null;

        if (!worldInteractTooltipRoot.activeSelf)
            worldInteractTooltipRoot.SetActive(true);

        PositionWorldInteractTooltip(screenPosition);
    }

    private void PositionWorldInteractTooltip(Vector2 screenPosition)
    {
        if (worldInteractTooltipRoot == null)
            return;

        RectTransform rootRect = worldInteractTooltipRoot.transform as RectTransform;
        RectTransform tooltipRect = rootRect;

        if (worldInteractTooltipRect != null)
        {
            bool pointsToChildOfRoot = rootRect != null &&
                                       worldInteractTooltipRect != rootRect &&
                                       worldInteractTooltipRect.IsChildOf(rootRect);

            if (!pointsToChildOfRoot)
                tooltipRect = worldInteractTooltipRect;
        }

        if (tooltipRect == null)
            return;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null)
            return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            uiCamera,
            out Vector2 localPoint
        );

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pivotFromBottomLeft = new Vector2(
            tooltipRect.rect.width * tooltipRect.pivot.x,
            tooltipRect.rect.height * tooltipRect.pivot.y
        );

        tooltipRect.anchoredPosition = localPoint + (worldInteractTooltipOffset / scale) + pivotFromBottomLeft;
    }

    private void HideWorldInteractTooltip()
    {
        if (worldInteractTooltipRoot != null && worldInteractTooltipRoot.activeSelf)
            worldInteractTooltipRoot.SetActive(false);
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (worldInteractTooltipRoot != null)
            {
                Transform tooltipTransform = worldInteractTooltipRoot.transform;
                Transform hitTransform = hitObject.transform;
                if (hitTransform == tooltipTransform || hitTransform.IsChildOf(tooltipTransform))
                    continue;
            }

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null)
            {
                if (!graphic.raycastTarget)
                    continue;

                if (graphic.color.a <= 0.001f)
                    continue;
            }

            CanvasGroup canvasGroup = hitObject.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && (!canvasGroup.blocksRaycasts || canvasGroup.alpha <= 0.001f))
                continue;

            return true;
        }

        return false;
    }

    private void HandleModalCloseHotkey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (!HasModalUiOpen())
            return;

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

    private void ResolvePlayerController()
    {
        if (playerController != null)
            return;

        playerController = FindFirstObjectByType<PlayerController>();
    }

    private void UpdateMovementLockState()
    {
        ResolvePlayerController();
        if (playerController == null)
            return;

        bool shouldLockMovement = HasModalUiOpen();
        playerController.SetMovementLocked(shouldLockMovement);
    }

    private bool HasModalUiOpen()
    {
        return stationCraftingModalOpen || currentOpenedChest != null || currentOpenedFurnace != null || currentOpenedBeacon != null;
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
        UpdateVisibility();
    }

    private void CloseModalUi()
    {
        if (currentOpenedChest != null && currentOpenedChest.IsOpen)
            currentOpenedChest.Close();

        if (currentOpenedFurnace != null && currentOpenedFurnace.IsOpen)
            currentOpenedFurnace.Close();

        currentOpenedChest = null;
        currentOpenedFurnace = null;
        CloseBeaconModal();

        if (stationCraftingModalOpen)
            CloseStationModal();

        CloseModalInventoryIfNeeded();
        UpdateMovementLockState();
    }

    private void CloseStationModal()
    {
        stationCraftingModalOpen = false;
        currentOpenedStation = null;

        if (craftingMenuUI != null)
            craftingMenuUI.Close();

        if (!HasModalUiOpen())
            CloseModalInventoryIfNeeded();

        UpdateMovementLockState();
    }

    private void CloseBeaconModal()
    {
        if (beaconUpgradeUI != null)
            beaconUpgradeUI.HideBeacon();

        currentOpenedBeacon = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        EnsureBeaconUpgradeUiOnScene();
    }

    private void EnsureBeaconUpgradeUiOnScene()
    {
        if (beaconUpgradeUI != null)
            return;

        Transform existing = transform.Find("BeaconUpgradePanel");
        if (existing != null)
        {
            beaconUpgradeUI = existing.GetComponent<BeaconUpgradeUI>();
            if (beaconUpgradeUI != null)
                return;
        }

        if (!(transform is RectTransform))
            return;

        GameObject panelObject = new GameObject(
            "BeaconUpgradePanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(BeaconUpgradeUI)
        );
        panelObject.transform.SetParent(transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(420f, 230f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.11f, 0.11f, 0.11f, 0.82f);
        panelImage.raycastTarget = true;

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        beaconUpgradeUI = panelObject.GetComponent<BeaconUpgradeUI>();
    }
#endif

    private FurnaceStation ResolveFurnaceForStation(CraftingStation station)
    {
        if (station == null)
            return null;

        FurnaceStation furnace = station.GetComponent<FurnaceStation>();
        if (furnace == null)
            furnace = station.gameObject.AddComponent<FurnaceStation>();

        return furnace;
    }

    public Transform DragParent => dragParent;

    public bool IsChestOpenForTransfers()
    {
        return currentOpenedChest != null && currentOpenedChest.IsOpen;
    }

    public bool IsFurnaceOpenForTransfers()
    {
        return currentOpenedFurnace != null && currentOpenedFurnace.IsOpen;
    }

    public bool TryTransferFromPlayerSlot(int playerSlotIndex, bool wholeStack)
    {
        if (IsChestOpenForTransfers())
            return TryTransferFromPlayerSlotToChest(playerSlotIndex, wholeStack);

        if (IsFurnaceOpenForTransfers())
            return TryTransferFromPlayerSlotToFurnace(playerSlotIndex, wholeStack);

        return false;
    }

    private bool TryTransferFromPlayerSlotToChest(int playerSlotIndex, bool wholeStack)
    {
        if (!IsChestOpenForTransfers() || inventory == null)
            return false;

        InventoryItem sourceSlot = inventory.GetItem(playerSlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0)
            return false;

        int requestAmount = wholeStack ? sourceSlot.amount : 1;
        if (!currentOpenedChest.CanAddItem(sourceSlot.item, requestAmount))
            return false;

        if (!inventory.TryRemoveFromSlot(playerSlotIndex, requestAmount, out ItemData removedItem, out int removedAmount))
            return false;

        if (removedItem == null || removedAmount <= 0)
            return false;

        if (currentOpenedChest.Add(removedItem, removedAmount))
            return true;

        inventory.Add(removedItem, removedAmount);
        return false;
    }

    public bool TryTransferFromPlayerSlotToFurnace(int playerSlotIndex, bool wholeStack)
    {
        if (!IsFurnaceOpenForTransfers() || inventory == null)
            return false;

        InventoryItem sourceSlot = inventory.GetItem(playerSlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0)
            return false;

        ItemData sourceItem = sourceSlot.item;
        int requestAmount = wholeStack ? sourceSlot.amount : 1;

        FurnaceSlotType targetSlotType;
        if (!TryFindBestFurnaceSlot(sourceItem, out targetSlotType))
            return false;

        int maxToMove = requestAmount;
        InventoryItem targetSlot = currentOpenedFurnace.GetSlot(targetSlotType);
        if (targetSlot == null)
            return false;

        if (targetSlot.IsEmpty)
            maxToMove = Mathf.Min(maxToMove, Mathf.Max(1, sourceItem.maxStack));
        else
            maxToMove = Mathf.Min(maxToMove, Mathf.Max(0, Mathf.Max(1, sourceItem.maxStack) - targetSlot.amount));

        if (maxToMove <= 0)
            return false;

        if (!inventory.TryRemoveFromSlot(playerSlotIndex, maxToMove, out ItemData removedItem, out int removedAmount))
            return false;

        if (removedItem == null || removedAmount <= 0)
            return false;

        if (currentOpenedFurnace.TryAddToSlot(targetSlotType, removedItem, removedAmount, out int addedAmount) && addedAmount > 0)
        {
            int leftover = removedAmount - addedAmount;
            if (leftover > 0)
                inventory.Add(removedItem, leftover);

            return true;
        }

        inventory.Add(removedItem, removedAmount);
        return false;
    }

    public bool TryTransferFromChestSlot(int chestSlotIndex, bool wholeStack)
    {
        if (!IsChestOpenForTransfers() || inventory == null)
            return false;

        InventoryItem sourceSlot = currentOpenedChest.GetItem(chestSlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0)
            return false;

        int requestAmount = wholeStack ? sourceSlot.amount : 1;
        if (!inventory.CanAddItem(sourceSlot.item, requestAmount))
            return false;

        if (!currentOpenedChest.TryRemoveFromSlot(chestSlotIndex, requestAmount, out ItemData removedItem, out int removedAmount))
            return false;

        if (removedItem == null || removedAmount <= 0)
            return false;

        inventory.Add(removedItem, removedAmount);
        return true;
    }

    public bool TryDropPlayerSlotToChestSlot(int playerSlotIndex, int chestSlotIndex)
    {
        if (!IsChestOpenForTransfers() || inventory == null)
            return false;

        InventoryItem sourceSlot = inventory.GetItem(playerSlotIndex);
        InventoryItem targetSlot = currentOpenedChest.GetItem(chestSlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0 || targetSlot == null)
            return false;

        ItemData sourceItem = sourceSlot.item;
        int sourceAmount = sourceSlot.amount;
        ItemData targetItem = targetSlot.item;
        int targetAmount = targetSlot.amount;

        if (targetSlot.IsEmpty)
        {
            if (!currentOpenedChest.TrySetSlot(chestSlotIndex, sourceItem, sourceAmount))
                return false;

            inventory.TrySetSlot(playerSlotIndex, null, 0);
            return true;
        }

        if (targetItem == sourceItem)
        {
            int maxStack = Mathf.Max(1, sourceItem.maxStack);
            int freeSpace = Mathf.Max(0, maxStack - targetAmount);
            if (freeSpace <= 0)
                return false;

            int moveAmount = Mathf.Min(freeSpace, sourceAmount);
            if (!currentOpenedChest.TryAddToSlot(chestSlotIndex, sourceItem, moveAmount, out int addedAmount) || addedAmount <= 0)
                return false;

            inventory.TryRemoveFromSlot(playerSlotIndex, addedAmount, out _, out _);
            return true;
        }

        if (!inventory.TrySetSlot(playerSlotIndex, targetItem, targetAmount))
            return false;

        if (currentOpenedChest.TrySetSlot(chestSlotIndex, sourceItem, sourceAmount))
            return true;

        inventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount);
        return false;
    }

    public bool TryDropPlayerSlotToFurnaceSlot(int playerSlotIndex, int furnaceSlotIndex)
    {
        if (!IsFurnaceOpenForTransfers() || inventory == null)
            return false;

        FurnaceSlotType slotType = ToFurnaceSlotType(furnaceSlotIndex);
        InventoryItem sourceSlot = inventory.GetItem(playerSlotIndex);
        InventoryItem targetSlot = currentOpenedFurnace.GetSlot(slotType);

        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0 || targetSlot == null)
            return false;

        ItemData sourceItem = sourceSlot.item;
        int sourceAmount = sourceSlot.amount;

        if (!currentOpenedFurnace.CanAcceptItem(slotType, sourceItem))
            return false;

        if (targetSlot.IsEmpty)
        {
            if (!currentOpenedFurnace.TrySetSlot(slotType, sourceItem, sourceAmount))
                return false;

            inventory.TrySetSlot(playerSlotIndex, null, 0);
            return true;
        }

        if (targetSlot.item == sourceItem)
        {
            int maxStack = Mathf.Max(1, sourceItem.maxStack);
            int freeSpace = Mathf.Max(0, maxStack - targetSlot.amount);
            if (freeSpace <= 0)
                return false;

            int moveAmount = Mathf.Min(freeSpace, sourceAmount);
            if (!currentOpenedFurnace.TryAddToSlot(slotType, sourceItem, moveAmount, out int addedAmount) || addedAmount <= 0)
                return false;

            inventory.TryRemoveFromSlot(playerSlotIndex, addedAmount, out _, out _);
            return true;
        }

        if (!currentOpenedFurnace.CanAddToSlot(slotType, sourceItem, sourceAmount))
            return false;

        if (!inventory.TrySetSlot(playerSlotIndex, targetSlot.item, targetSlot.amount))
            return false;

        if (currentOpenedFurnace.TrySetSlot(slotType, sourceItem, sourceAmount))
            return true;

        inventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount);
        return false;
    }

    public bool TryDropChestSlotToPlayerSlot(int chestSlotIndex, int playerSlotIndex)
    {
        if (!IsChestOpenForTransfers() || inventory == null)
            return false;

        InventoryItem sourceSlot = currentOpenedChest.GetItem(chestSlotIndex);
        InventoryItem targetSlot = inventory.GetItem(playerSlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0 || targetSlot == null)
            return false;

        ItemData sourceItem = sourceSlot.item;
        int sourceAmount = sourceSlot.amount;
        ItemData targetItem = targetSlot.item;
        int targetAmount = targetSlot.amount;

        if (targetSlot.IsEmpty)
        {
            if (!inventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount))
                return false;

            currentOpenedChest.TrySetSlot(chestSlotIndex, null, 0);
            return true;
        }

        if (targetItem == sourceItem)
        {
            int maxStack = Mathf.Max(1, sourceItem.maxStack);
            int freeSpace = Mathf.Max(0, maxStack - targetAmount);
            if (freeSpace <= 0)
                return false;

            int moveAmount = Mathf.Min(freeSpace, sourceAmount);
            if (!inventory.TryAddToSlot(playerSlotIndex, sourceItem, moveAmount, out int addedAmount) || addedAmount <= 0)
                return false;

            currentOpenedChest.TryRemoveFromSlot(chestSlotIndex, addedAmount, out _, out _);
            return true;
        }

        if (!currentOpenedChest.TrySetSlot(chestSlotIndex, targetItem, targetAmount))
            return false;

        if (inventory.TrySetSlot(playerSlotIndex, sourceItem, sourceAmount))
            return true;

        currentOpenedChest.TrySetSlot(chestSlotIndex, sourceItem, sourceAmount);
        return false;
    }

    private bool TryFindBestFurnaceSlot(ItemData item, out FurnaceSlotType slotType)
    {
        slotType = FurnaceSlotType.Input;

        if (item == null || currentOpenedFurnace == null)
            return false;

        bool isFuel = item.furnaceFuelSeconds > 0f;
        bool isSmeltable = item.furnaceSmeltResult != null;

        if (!isFuel && !isSmeltable)
            return false;

        if (isFuel && currentOpenedFurnace.CanAddToSlot(FurnaceSlotType.Fuel, item, 1))
        {
            slotType = FurnaceSlotType.Fuel;
            return true;
        }

        if (isSmeltable && currentOpenedFurnace.CanAddToSlot(FurnaceSlotType.Input, item, 1))
        {
            slotType = FurnaceSlotType.Input;
            return true;
        }

        if (isFuel && currentOpenedFurnace.CanAcceptItem(FurnaceSlotType.Fuel, item) &&
            currentOpenedFurnace.GetSlot(FurnaceSlotType.Fuel).IsEmpty)
        {
            slotType = FurnaceSlotType.Fuel;
            return true;
        }

        if (isSmeltable && currentOpenedFurnace.CanAcceptItem(FurnaceSlotType.Input, item) &&
            currentOpenedFurnace.GetSlot(FurnaceSlotType.Input).IsEmpty)
        {
            slotType = FurnaceSlotType.Input;
            return true;
        }

        return false;
    }

    private static FurnaceSlotType ToFurnaceSlotType(int slotIndex)
    {
        return slotIndex switch
        {
            0 => FurnaceSlotType.Fuel,
            1 => FurnaceSlotType.Input,
            _ => FurnaceSlotType.Output,
        };
    }
}
