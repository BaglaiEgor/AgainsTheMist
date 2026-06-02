public static class ItemRoutingService
{
    public static bool TryFastTransfer(ItemSlotRef source, InventoryUI ui)
    {
        InventoryItem stack = source.container.GetItem(source.slotIndex);
        if (stack == null || stack.IsEmpty)
            return false;

        Inventory player = ui.PlayerInventory;
        EquipmentInventory equipment = ui.PlayerEquipment;
        ChestInventory chest = ui.CurrentOpenedChest;
        FurnaceStation furnace = ui.CurrentOpenedFurnace;

        if (ReferenceEquals(source.container, player))
            return RouteFromPlayer(source, stack, player, chest, furnace);

        if (ReferenceEquals(source.container, chest))
            return MoveAll(source, player);

        if (ReferenceEquals(source.container, furnace))
            return MoveAll(source, player);

        if (ReferenceEquals(source.container, equipment))
            return MoveAll(source, player);

        return false;
    }

    private static bool RouteFromPlayer(
        ItemSlotRef source,
        InventoryItem stack,
        Inventory player,
        ChestInventory chest,
        FurnaceStation furnace)
    {
        if (chest != null && chest.IsOpen)
            return MoveAll(source, chest);

        if (furnace != null && furnace.IsOpen)
            return MoveToFurnace(source, stack, furnace);

        if (stack.item.type == ItemType.Equipment)
            return player.TryEquipFromSlot(source.slotIndex);

        return false;
    }

    private static bool MoveToFurnace(ItemSlotRef source, InventoryItem stack, FurnaceStation furnace)
    {
        ItemData item = stack.item;
        int fuelSlot = FurnaceStation.ToSlotIndex(FurnaceSlotType.Fuel);
        int inputSlot = FurnaceStation.ToSlotIndex(FurnaceSlotType.Input);

        if (furnace.CanAccept(fuelSlot, item))
            return InventoryTransactionService.TryMove(source.container, source.slotIndex, furnace, fuelSlot, stack.amount);

        if (furnace.CanAccept(inputSlot, item))
            return InventoryTransactionService.TryMove(source.container, source.slotIndex, furnace, inputSlot, stack.amount);

        return false;
    }

    private static bool MoveAll(ItemSlotRef source, IItemContainer target)
    {
        InventoryItem stack = source.container.GetItem(source.slotIndex);
        return stack != null &&
               !stack.IsEmpty &&
               InventoryTransactionService.TryMoveToContainer(source.container, source.slotIndex, target, stack.amount);
    }
}
