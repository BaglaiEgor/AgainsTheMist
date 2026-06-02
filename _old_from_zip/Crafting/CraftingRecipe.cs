using UnityEngine;

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Farm/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public ItemData result;
    public int resultAmount = 1;

    public CraftStationType station = CraftStationType.None;
    public CraftingCategory category = CraftingCategory.Basic;

    [System.Serializable]
    public struct Ingredient
    {
        public ItemData item;
        public int amount;
    }

    public Ingredient[] ingredients;
}
