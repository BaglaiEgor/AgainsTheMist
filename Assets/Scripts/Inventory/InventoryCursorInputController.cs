using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryCursorInputController : MonoBehaviour
{
    private const float RightHoldInitialDelay = 0.28f;
    private const float RightHoldRepeatInterval = 0.08f;

    [SerializeField] private CursorStackController cursorController;

    private InventoryUI owner;
    private IItemContainer hoveredContainer;
    private int hoveredSlotIndex = -1;
    private float nextRightHoldActionTime;
    private IItemContainer rightHoldTakeContainer;
    private int rightHoldTakeSlotIndex = -1;
    private static int lastOutsideDropFrame = -1;

    public bool HasActiveIntent => cursorController != null && cursorController.HasActiveIntent;
    public static bool HandledOutsideDropThisFrame => lastOutsideDropFrame == Time.frameCount;

    public void Configure(CursorStackController controller)
    {
        if (controller != null)
            cursorController = controller;
    }

    public void Initialize(InventoryUI inventoryUi, Transform dragParent)
    {
        owner = inventoryUi;

        if (cursorController == null)
            return;

        cursorController.Initialize(dragParent);
    }

    public void Tick(PlayerController playerController)
    {
        HandleCursorDropOutsideUI(playerController);
        HandleRightClickHold();
    }

    public void SetHoveredSlot(IItemContainer container, int slotIndex)
    {
        hoveredContainer = container;
        hoveredSlotIndex = slotIndex;
    }

    public void ClearHoveredSlot(IItemContainer container, int slotIndex)
    {
        if (!ReferenceEquals(hoveredContainer, container) || hoveredSlotIndex != slotIndex)
            return;

        hoveredContainer = null;
        hoveredSlotIndex = -1;
    }

    public bool IsSourceSlot(IItemContainer container, int slotIndex)
    {
        return cursorController != null && cursorController.IsSourceSlot(container, slotIndex);
    }

    public void ResolveOnUiClose()
    {
        if (cursorController == null || !cursorController.HasActiveIntent)
            return;

        if (owner != null && cursorController.TryReturnToContainer(owner.PlayerInventory))
            owner.NotifySlotVisualRefresh();
    }

    public void HandleSlotLeftClick(IItemContainer container, int slotIndex)
    {
        if (cursorController == null || container == null || !container.IsValidSlot(slotIndex))
            return;

        bool shouldRefresh = false;

        if (!cursorController.HasActiveIntent)
        {
            InventoryItem sourceSlot = container.GetItem(slotIndex);
            if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.item == null || sourceSlot.amount <= 0)
                return;

            shouldRefresh = cursorController.TryBeginIntent(container, slotIndex, sourceSlot.amount);
            if (shouldRefresh)
                owner?.NotifySlotVisualRefresh();
            return;
        }

        if (!cursorController.TryGetIntent(out IItemContainer source, out int sourceSlotIndex, out int requestedAmount, out ItemData item))
            return;

        InventoryItem targetSlot = container.GetItem(slotIndex);
        if (targetSlot == null)
            return;

        if (targetSlot.IsEmpty)
        {
            if (InventoryTransactionService.TryMove(source, sourceSlotIndex, container, slotIndex, requestedAmount))
            {
                owner?.NotifySlotVisualRefresh();
            }

            return;
        }

        if (targetSlot.item == item)
        {
            int freeSpace = Mathf.Max(0, Mathf.Max(1, item.maxStack) - targetSlot.amount);
            int moveAmount = Mathf.Min(requestedAmount, freeSpace);
            if (moveAmount <= 0)
                return;

            if (InventoryTransactionService.TryMerge(source, sourceSlotIndex, container, slotIndex, moveAmount))
            {
                owner?.NotifySlotVisualRefresh();
            }

            return;
        }

        if (InventoryTransactionService.TrySwap(source, sourceSlotIndex, container, slotIndex))
        {
            owner?.NotifySlotVisualRefresh();
        }
    }

    public void HandleSlotRightClick(IItemContainer container, int slotIndex)
    {
        nextRightHoldActionTime = Time.unscaledTime + RightHoldInitialDelay;
        rightHoldTakeContainer = null;
        rightHoldTakeSlotIndex = -1;

        if (cursorController == null || container == null || !container.IsValidSlot(slotIndex))
            return;

        if (!cursorController.HasActiveIntent)
        {
            if (cursorController.TryBeginIntent(container, slotIndex, 1))
            {
                rightHoldTakeContainer = container;
                rightHoldTakeSlotIndex = slotIndex;
                owner?.NotifySlotVisualRefresh();
            }
            return;
        }

        if (!cursorController.TryGetIntent(out IItemContainer source, out int sourceSlotIndex, out _, out ItemData item))
            return;

        InventoryItem targetSlot = container.GetItem(slotIndex);
        if (targetSlot == null)
            return;

        if (!targetSlot.IsEmpty && targetSlot.item == item)
        {
            if (TryTakeOneMoreFromSlot(container, slotIndex))
            {
                rightHoldTakeContainer = container;
                rightHoldTakeSlotIndex = slotIndex;
                owner?.NotifySlotVisualRefresh();
            }

            return;
        }

        if (!targetSlot.IsEmpty)
            return;

        if (InventoryTransactionService.TrySplitOne(source, sourceSlotIndex, container, slotIndex))
        {
            owner?.NotifySlotVisualRefresh();
        }
    }

    public bool TryGetCursorStack(out ItemData item, out int amount)
    {
        item = null;
        amount = 0;
        return cursorController != null && cursorController.TryGetStack(out item, out amount);
    }

    public bool TryConsumeCursorItem(int amount)
    {
        if (cursorController == null)
            return false;

        bool consumed = cursorController.TryConsume(amount);
        if (consumed)
            owner?.NotifySlotVisualRefresh();

        return consumed;
    }

    private void HandleCursorDropOutsideUI(PlayerController playerController)
    {
        if (cursorController == null || !cursorController.HasActiveIntent)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            return;

        if (UiInputBlocker.IsPointerOverBlockingUI())
            return;

        lastOutsideDropFrame = Time.frameCount;

        if (playerController == null)
            return;

        if (!cursorController.TryGetIntent(out IItemContainer source, out int sourceSlotIndex, out int requestedAmount, out ItemData item))
            return;

        ItemDropRequest request = new ItemDropRequest(
            playerController.transform,
            playerController.GetDropFacingDirection(),
            item,
            requestedAmount
        );

        if (InventoryTransactionService.TryDrop(source, sourceSlotIndex, requestedAmount, request))
        {
            owner?.NotifySlotVisualRefresh();
        }
    }

    private void HandleRightClickHold()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.isPressed)
        {
            nextRightHoldActionTime = 0f;
            rightHoldTakeContainer = null;
            rightHoldTakeSlotIndex = -1;
            return;
        }

        if (cursorController == null || !cursorController.HasActiveIntent)
            return;
        if (hoveredContainer == null || !hoveredContainer.IsValidSlot(hoveredSlotIndex))
            return;
        if (Time.unscaledTime < nextRightHoldActionTime)
            return;

        if (ReferenceEquals(hoveredContainer, rightHoldTakeContainer) && hoveredSlotIndex == rightHoldTakeSlotIndex)
        {
            if (TryTakeOneMoreFromSlot(hoveredContainer, hoveredSlotIndex))
                owner?.NotifySlotVisualRefresh();
        }
        else
        {
            HandleSlotRightClick(hoveredContainer, hoveredSlotIndex);
        }

        nextRightHoldActionTime = Time.unscaledTime + RightHoldRepeatInterval;
    }

    private bool TryTakeOneMoreFromSlot(IItemContainer container, int slotIndex)
    {
        if (cursorController == null || container == null || !container.IsValidSlot(slotIndex))
            return false;

        if (!cursorController.TryGetIntent(out _, out _, out _, out ItemData cursorItem))
            return false;

        InventoryItem slot = container.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item != cursorItem)
            return false;

        return InventoryTransactionService.TrySplitOne(container, slotIndex, cursorController, 0);
    }
}
