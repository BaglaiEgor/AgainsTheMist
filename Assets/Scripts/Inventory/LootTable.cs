using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Inventory/Loot Table")]
public class LootTable : ScriptableObject
{
    [Serializable]
    public struct LootEntry
    {
        public ItemData item;
        [Min(1)] public int minAmount;
        [Min(1)] public int maxAmount;
        [Range(0f, 1f)] public float chance;
    }

    [SerializeField] private LootEntry[] entries = Array.Empty<LootEntry>();

    public LootEntry[] Entries => entries;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (entries == null)
            entries = Array.Empty<LootEntry>();

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].minAmount <= 0)
                entries[i].minAmount = 1;

            if (entries[i].maxAmount < entries[i].minAmount)
                entries[i].maxAmount = entries[i].minAmount;

            entries[i].chance = Mathf.Clamp01(entries[i].chance);
        }
    }
#endif
}
