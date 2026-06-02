using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Farm/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new();

    public IReadOnlyList<ItemData> AllItems => items;

    public ItemData FindById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item != null && item.itemID == itemId)
                return item;
        }

        return null;
    }
}
