using UnityEngine;
using System.Collections.Generic;

public class CraftingManager : MonoBehaviour
{
    [SerializeField] private List<CraftingRecipe> recipes;
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private CursorStackController cursorStack;
    [SerializeField] private float stationCheckRadius = 2f;
    [SerializeField] private LayerMask stationLayer;

    private CraftStationType currentStation = CraftStationType.None;

    public IReadOnlyList<CraftingRecipe> AllRecipes => recipes;

    void Update()
    {
        UpdateCurrentStation();
    }

    void UpdateCurrentStation()
    {
        currentStation = CraftStationType.None;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            stationCheckRadius,
            stationLayer
        );

        foreach (var hit in hits)
        {
            CraftingStation station = hit.GetComponent<CraftingStation>();
            if (station != null)
            {
                currentStation = station.stationType;
                return;
            }
        }
    }

    public bool CanCraft(CraftingRecipe recipe)
    {
        return CanCraft(recipe, currentStation);
    }

    public bool CanCraft(CraftingRecipe recipe, CraftStationType stationContext)
    {
        ResolveCursorStack();

        if (recipe == null || playerInventory == null)
            return false;

        if (recipe.station != CraftStationType.None &&
            recipe.station != stationContext)
            return false;

        if (GetCursorCraftCapacity(recipe) <= 0 && !CanCraftToInventory(recipe))
            return false;

        if (recipe.ingredients == null || recipe.ingredients.Length == 0)
            return true;

        foreach (var ing in recipe.ingredients)
        {
            if (ing.item == null || ing.amount <= 0)
                continue;

            if (!playerInventory.HasItem(ing.item, ing.amount))
                return false;
        }

        return true;
    }

    public bool Craft(CraftingRecipe recipe)
    {
        return Craft(recipe, currentStation);
    }

    public bool Craft(CraftingRecipe recipe, CraftStationType stationContext)
    {
        return Craft(recipe, stationContext, 1);
    }

    public bool Craft(CraftingRecipe recipe, CraftStationType stationContext, int craftCount)
    {
        ResolveCursorStack();

        int maxCraftCount = GetMaxCraftCount(recipe, stationContext);
        int count = Mathf.Clamp(craftCount, 1, maxCraftCount);
        if (count <= 0)
            return false;

        return InventoryTransactionService.TryCraft(playerInventory, recipe, stationContext, cursorStack, count);
    }

    public bool CraftMax(CraftingRecipe recipe, CraftStationType stationContext)
    {
        return Craft(recipe, stationContext, GetMaxCraftCount(recipe, stationContext));
    }

    public bool CraftToInventory(CraftingRecipe recipe, CraftStationType stationContext)
    {
        return InventoryTransactionService.TryCraft(playerInventory, recipe, stationContext);
    }

    private int GetMaxCraftCount(CraftingRecipe recipe, CraftStationType stationContext)
    {
        if (recipe == null || playerInventory == null)
            return 0;
        if (recipe.station != CraftStationType.None && recipe.station != stationContext)
            return 0;

        int maxByCursor = GetCursorCraftCapacity(recipe);
        if (maxByCursor <= 0)
            return 0;

        int maxByIngredients = int.MaxValue;
        if (recipe.ingredients != null)
        {
            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                CraftingRecipe.Ingredient ingredient = recipe.ingredients[i];
                if (ingredient.item == null || ingredient.amount <= 0)
                    continue;

                int available = CountItem(playerInventory, ingredient.item);
                maxByIngredients = Mathf.Min(maxByIngredients, available / ingredient.amount);
            }
        }

        if (maxByIngredients == int.MaxValue)
            maxByIngredients = maxByCursor;

        return Mathf.Min(maxByIngredients, maxByCursor);
    }

    private int GetCursorCraftCapacity(CraftingRecipe recipe)
    {
        if (recipe == null || recipe.result == null || recipe.resultAmount <= 0)
            return 0;

        ResolveCursorStack();
        if (cursorStack == null)
            return 0;

        int maxStack = MaxStack(recipe.result);
        InventoryItem cursorItem = cursorStack.GetItem(0);
        if (cursorItem == null || cursorItem.IsEmpty)
            return maxStack / recipe.resultAmount;

        if (cursorItem.item != recipe.result)
            return 0;

        int freeSpace = Mathf.Max(0, maxStack - cursorItem.amount);
        return freeSpace / recipe.resultAmount;
    }

    private bool CanCraftToInventory(CraftingRecipe recipe)
    {
        return recipe != null &&
               playerInventory != null &&
               playerInventory.CanAddItem(recipe.result, recipe.resultAmount);
    }

    private void ResolveCursorStack()
    {
        if (cursorStack == null)
            cursorStack = FindFirstObjectByType<CursorStackController>();
    }

    private static int CountItem(Inventory inventory, ItemData item)
    {
        if (inventory == null || item == null)
            return 0;

        int total = 0;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventoryItem slot = inventory.GetItem(i);
            if (slot != null && !slot.IsEmpty && slot.item == item)
                total += slot.amount;
        }

        return total;
    }

    private static int MaxStack(ItemData item)
    {
        if (item == null)
            return 1;

        if (item.type == ItemType.Equipment)
            return 1;

        return Mathf.Max(1, item.maxStack);
    }

    public CraftStationType CurrentStation => currentStation;
    public Inventory PlayerInventory => playerInventory;
}
