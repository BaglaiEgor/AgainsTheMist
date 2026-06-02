using System.Collections.Generic;
using UnityEngine;

public static class InventoryTransactionService
{
    private struct SlotChange
    {
        public int slot;
        public ItemData item;
        public int amount;

        public SlotChange(int slot, ItemData item, int amount)
        {
            this.slot = slot;
            this.item = item;
            this.amount = Mathf.Max(0, amount);
        }
    }

    public static bool TryMove(IItemContainer from, int fromSlot, IItemContainer to, int toSlot, int amount)
    {
        if (!ReadStack(from, fromSlot, out ItemData item, out int fromAmount))
            return false;
        if (amount <= 0 || amount > fromAmount || SameSlot(from, fromSlot, to, toSlot))
            return false;

        InventoryItem target = to.GetItem(toSlot);
        if (!CanPlace(to, toSlot, target, item, amount))
            return false;

        Begin(from, to);
        SetAmount(from, fromSlot, item, fromAmount - amount);
        SetAmount(to, toSlot, item, (target == null || target.IsEmpty ? 0 : target.amount) + amount);
        End(from, to);
        return true;
    }

    public static bool TryMerge(IItemContainer from, int fromSlot, IItemContainer to, int toSlot, int amount)
    {
        return TryMove(from, fromSlot, to, toSlot, amount);
    }

    public static bool TrySplitOne(IItemContainer from, int fromSlot, IItemContainer to, int toSlot)
    {
        return TryMove(from, fromSlot, to, toSlot, 1);
    }

    public static bool TrySwap(IItemContainer a, int aSlot, IItemContainer b, int bSlot)
    {
        if (!ReadStack(a, aSlot, out ItemData aItem, out int aAmount))
            return false;
        if (!ReadStack(b, bSlot, out ItemData bItem, out int bAmount))
            return TryMove(a, aSlot, b, bSlot, aAmount);
        if (SameSlot(a, aSlot, b, bSlot))
            return false;
        if (!CanSet(a, aSlot, bItem, bAmount) || !CanSet(b, bSlot, aItem, aAmount))
            return false;

        Begin(a, b);
        SetAmount(a, aSlot, bItem, bAmount);
        SetAmount(b, bSlot, aItem, aAmount);
        End(a, b);
        return true;
    }

    public static bool TryMoveToContainer(IItemContainer from, int fromSlot, IItemContainer to, int amount)
    {
        if (!ReadStack(from, fromSlot, out ItemData item, out int fromAmount))
            return false;
        if (amount <= 0 || amount > fromAmount)
            return false;
        if (!BuildInsertPlan(to, item, amount, from, fromSlot, out List<SlotChange> insertPlan))
            return false;

        Begin(from, to);
        SetAmount(from, fromSlot, item, fromAmount - amount);
        Apply(to, insertPlan);
        End(from, to);
        return true;
    }

    public static bool TryInsertItem(IItemContainer container, ItemData item, int amount)
    {
        if (!BuildInsertPlan(container, item, amount, null, -1, out List<SlotChange> insertPlan))
            return false;

        container.BeginTransaction();
        Apply(container, insertPlan);
        container.EndTransaction();
        return true;
    }

    public static bool TryDrop(IItemContainer from, int fromSlot, int amount, ItemDropRequest dropRequest)
    {
        if (!ReadStack(from, fromSlot, out ItemData item, out int fromAmount))
            return false;
        if (amount <= 0 || amount > fromAmount)
            return false;
        if (!WorldItemDropService.TryResolveDropPosition(dropRequest, out Vector3 dropPosition))
            return false;

        from.BeginTransaction();
        SetAmount(from, fromSlot, item, fromAmount - amount);

        GameObject spawned = WorldItemDropService.SpawnDrop(item, amount, dropPosition);
        if (spawned == null)
        {
            SetAmount(from, fromSlot, item, fromAmount);
            from.EndTransaction();
            return false;
        }

        from.EndTransaction();
        return true;
    }

    public static bool TryConsume(IItemContainer container, ItemData item, int amount)
    {
        RequiredItem[] requirements = { new RequiredItem(item, amount) };
        return TryConsume(container, requirements);
    }

    public static bool TryConsume(IItemContainer container, IReadOnlyList<RequiredItem> requirements)
    {
        if (!BuildConsumePlan(container, requirements, out List<SlotChange> consumePlan))
            return false;

        container.BeginTransaction();
        Apply(container, consumePlan);
        container.EndTransaction();
        return true;
    }

    public static bool TryCraft(Inventory inventory, CraftingRecipe recipe, CraftStationType stationContext)
    {
        if (recipe == null || recipe.result == null || recipe.resultAmount <= 0)
            return false;
        if (recipe.station != CraftStationType.None && recipe.station != stationContext)
            return false;

        List<RequiredItem> ingredients = RecipeIngredients(recipe);
        if (!BuildConsumePlan(inventory, ingredients, out List<SlotChange> consumePlan))
            return false;

        ItemStackSnapshot[] before = Snapshot(inventory);

        inventory.BeginTransaction();
        Apply(inventory, consumePlan);

        bool canAddResult = BuildInsertPlan(inventory, recipe.result, recipe.resultAmount, null, -1, out List<SlotChange> resultPlan);
        if (canAddResult)
            Apply(inventory, resultPlan);
        else
            Restore(inventory, before);

        inventory.EndTransaction();
        return canAddResult;
    }

    public static bool TryCraft(
        Inventory inventory,
        CraftingRecipe recipe,
        CraftStationType stationContext,
        IItemContainer resultContainer,
        int craftCount)
    {
        if (recipe == null || recipe.result == null || recipe.resultAmount <= 0 || craftCount <= 0)
            return false;
        if (inventory == null || resultContainer == null)
            return false;
        if (recipe.station != CraftStationType.None && recipe.station != stationContext)
            return false;
        if (ReferenceEquals(inventory, resultContainer))
            return TryCraft(inventory, recipe, stationContext);

        List<RequiredItem> ingredients = RecipeIngredients(recipe);
        List<RequiredItem> multipliedIngredients = MultiplyRequirements(ingredients, craftCount);
        if (!BuildConsumePlan(inventory, multipliedIngredients, out List<SlotChange> consumePlan))
            return false;

        int resultAmount = recipe.resultAmount * craftCount;
        if (!BuildInsertPlan(resultContainer, recipe.result, resultAmount, null, -1, out List<SlotChange> resultPlan))
            return false;

        Begin(inventory, resultContainer);
        Apply(inventory, consumePlan);
        Apply(resultContainer, resultPlan);
        End(inventory, resultContainer);
        return true;
    }

    public static bool TryBeaconUpgrade(BeaconUpgrade beacon, Inventory inventory)
    {
        if (!beacon.TryGetNextLevel(out BeaconUpgrade.UpgradeLevel nextLevel))
            return false;

        List<RequiredItem> cost = BeaconCost(nextLevel);
        if (!BuildConsumePlan(inventory, cost, out List<SlotChange> consumePlan))
            return false;

        ItemStackSnapshot[] inventoryBefore = Snapshot(inventory);
        BeaconUpgradeSnapshot beaconBefore = beacon.CreateSnapshot();

        inventory.BeginTransaction();
        Apply(inventory, consumePlan);

        bool upgraded = beacon.ApplyUpgradeFromTransaction(nextLevel);
        if (!upgraded)
        {
            Restore(inventory, inventoryBefore);
            beacon.RestoreSnapshot(beaconBefore);
        }

        inventory.EndTransaction();
        return upgraded;
    }

    private static bool ReadStack(IItemContainer container, int slot, out ItemData item, out int amount)
    {
        item = null;
        amount = 0;

        if (container == null || !container.IsValidSlot(slot) || !container.CanExtract(slot))
            return false;

        InventoryItem stack = container.GetItem(slot);
        if (stack == null || stack.IsEmpty)
            return false;

        item = stack.item;
        amount = stack.amount;
        return item != null && amount > 0;
    }

    private static bool CanPlace(IItemContainer container, int slot, InventoryItem target, ItemData item, int amount)
    {
        if (container == null || item == null || amount <= 0)
            return false;
        if (!container.IsValidSlot(slot) || !container.CanAccept(slot, item))
            return false;
        if (target == null || target.IsEmpty)
            return amount <= MaxStack(item);
        if (target.item != item)
            return false;

        return target.amount + amount <= MaxStack(item);
    }

    private static bool CanSet(IItemContainer container, int slot, ItemData item, int amount)
    {
        return item != null &&
               amount > 0 &&
               amount <= MaxStack(item) &&
               container.IsValidSlot(slot) &&
               container.CanAccept(slot, item);
    }

    private static bool BuildInsertPlan(
        IItemContainer container,
        ItemData item,
        int amount,
        IItemContainer excludedContainer,
        int excludedSlot,
        out List<SlotChange> plan)
    {
        plan = new List<SlotChange>();
        if (container == null || item == null || amount <= 0)
            return false;

        int remaining = amount;

        AddToExistingStacks(container, item, excludedContainer, excludedSlot, plan, ref remaining);
        AddToEmptySlots(container, item, excludedContainer, excludedSlot, plan, ref remaining);

        if (remaining <= 0)
            return true;

        plan.Clear();
        return false;
    }

    private static void AddToExistingStacks(
        IItemContainer container,
        ItemData item,
        IItemContainer excludedContainer,
        int excludedSlot,
        List<SlotChange> plan,
        ref int remaining)
    {
        for (int i = 0; i < container.SlotCount && remaining > 0; i++)
        {
            if (SameSlot(container, i, excludedContainer, excludedSlot))
                continue;

            InventoryItem slot = container.GetItem(i);
            if (slot == null || slot.IsEmpty || slot.item != item || !container.CanAccept(i, item))
                continue;

            int add = Mathf.Min(MaxStack(item) - slot.amount, remaining);
            if (add <= 0)
                continue;

            plan.Add(new SlotChange(i, item, slot.amount + add));
            remaining -= add;
        }
    }

    private static void AddToEmptySlots(
        IItemContainer container,
        ItemData item,
        IItemContainer excludedContainer,
        int excludedSlot,
        List<SlotChange> plan,
        ref int remaining)
    {
        for (int i = 0; i < container.SlotCount && remaining > 0; i++)
        {
            if (SameSlot(container, i, excludedContainer, excludedSlot))
                continue;

            InventoryItem slot = container.GetItem(i);
            if (slot == null || !slot.IsEmpty || !container.CanAccept(i, item))
                continue;

            int add = Mathf.Min(MaxStack(item), remaining);
            plan.Add(new SlotChange(i, item, add));
            remaining -= add;
        }
    }

    private static bool BuildConsumePlan(IItemContainer container, IReadOnlyList<RequiredItem> requirements, out List<SlotChange> plan)
    {
        plan = new List<SlotChange>();
        if (container == null)
            return false;
        if (requirements == null || requirements.Count == 0)
            return true;

        Dictionary<ItemData, int> needed = MergeRequirements(requirements);
        foreach (KeyValuePair<ItemData, int> pair in needed)
        {
            int remaining = pair.Value;
            for (int i = 0; i < container.SlotCount && remaining > 0; i++)
            {
                InventoryItem slot = container.GetItem(i);
                if (slot == null || slot.IsEmpty || slot.item != pair.Key || !container.CanExtract(i))
                    continue;

                int take = Mathf.Min(slot.amount, remaining);
                plan.Add(new SlotChange(i, slot.item, slot.amount - take));
                remaining -= take;
            }

            if (remaining > 0)
            {
                plan.Clear();
                return false;
            }
        }

        return true;
    }

    private static Dictionary<ItemData, int> MergeRequirements(IReadOnlyList<RequiredItem> requirements)
    {
        Dictionary<ItemData, int> result = new Dictionary<ItemData, int>();
        for (int i = 0; i < requirements.Count; i++)
        {
            RequiredItem requirement = requirements[i];
            if (requirement.item == null || requirement.amount <= 0)
                continue;

            result.TryGetValue(requirement.item, out int current);
            result[requirement.item] = current + requirement.amount;
        }

        return result;
    }

    private static List<RequiredItem> RecipeIngredients(CraftingRecipe recipe)
    {
        List<RequiredItem> result = new List<RequiredItem>();
        if (recipe.ingredients == null)
            return result;

        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            CraftingRecipe.Ingredient ingredient = recipe.ingredients[i];
            result.Add(new RequiredItem(ingredient.item, ingredient.amount));
        }

        return result;
    }

    private static List<RequiredItem> MultiplyRequirements(IReadOnlyList<RequiredItem> requirements, int multiplier)
    {
        List<RequiredItem> result = new List<RequiredItem>();
        if (requirements == null || multiplier <= 0)
            return result;

        for (int i = 0; i < requirements.Count; i++)
        {
            RequiredItem requirement = requirements[i];
            result.Add(new RequiredItem(requirement.item, requirement.amount * multiplier));
        }

        return result;
    }

    private static List<RequiredItem> BeaconCost(BeaconUpgrade.UpgradeLevel level)
    {
        List<RequiredItem> result = new List<RequiredItem>();
        if (level == null || level.requiredResources == null)
            return result;

        for (int i = 0; i < level.requiredResources.Length; i++)
        {
            BeaconUpgrade.RequiredResource requirement = level.requiredResources[i];
            if (requirement != null)
                result.Add(new RequiredItem(requirement.item, requirement.amount));
        }

        return result;
    }

    private static void Apply(IItemContainer container, List<SlotChange> changes)
    {
        for (int i = 0; i < changes.Count; i++)
        {
            SlotChange change = changes[i];
            SetAmount(container, change.slot, change.item, change.amount);
        }
    }

    private static void SetAmount(IItemContainer container, int slot, ItemData item, int amount)
    {
        container.SetSlotFromTransaction(slot, amount > 0 ? item : null, amount);
    }

    private static ItemStackSnapshot[] Snapshot(IItemContainer container)
    {
        ItemStackSnapshot[] snapshot = new ItemStackSnapshot[container.SlotCount];
        for (int i = 0; i < snapshot.Length; i++)
            snapshot[i] = ItemStackSnapshot.FromSlot(container.GetItem(i));

        return snapshot;
    }

    private static void Restore(IItemContainer container, ItemStackSnapshot[] snapshot)
    {
        for (int i = 0; i < snapshot.Length; i++)
            container.SetSlotFromTransaction(i, snapshot[i].item, snapshot[i].amount);
    }

    private static void Begin(IItemContainer a, IItemContainer b)
    {
        a.BeginTransaction();
        if (!ReferenceEquals(a, b))
            b.BeginTransaction();
    }

    private static void End(IItemContainer a, IItemContainer b)
    {
        if (!ReferenceEquals(a, b))
            b.EndTransaction();
        a.EndTransaction();
    }

    private static bool SameSlot(IItemContainer a, int aSlot, IItemContainer b, int bSlot)
    {
        return ReferenceEquals(a, b) && aSlot == bSlot;
    }

    private static int MaxStack(ItemData item)
    {
        if (item == null)
            return 1;

        if (item.type == ItemType.Equipment)
            return 1;

        return Mathf.Max(1, item.maxStack);
    }
}

public readonly struct RequiredItem
{
    public readonly ItemData item;
    public readonly int amount;

    public RequiredItem(ItemData item, int amount)
    {
        this.item = item;
        this.amount = Mathf.Max(0, amount);
    }
}
