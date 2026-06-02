using UnityEngine;
using System.Collections.Generic;

public class CraftingManager : MonoBehaviour
{
    [SerializeField] private List<CraftingRecipe> recipes;
    [SerializeField] private Inventory playerInventory;
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
        if (recipe == null || playerInventory == null)
            return false;

        if (recipe.station != CraftStationType.None &&
            recipe.station != stationContext)
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
        if (!CanCraft(recipe, stationContext))
            return false;

        if (recipe.ingredients != null)
        {
            foreach (var ing in recipe.ingredients)
            {
                if (ing.item == null || ing.amount <= 0)
                    continue;

                playerInventory.Remove(ing.item, ing.amount);
            }
        }

        if (recipe.result == null || recipe.resultAmount <= 0)
            return false;

        playerInventory.Add(recipe.result, recipe.resultAmount);
        return true;
    }

    public CraftStationType CurrentStation => currentStation;
    public Inventory PlayerInventory => playerInventory;
}
