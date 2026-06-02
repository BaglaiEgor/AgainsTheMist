using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class SaveablePlacedTileRegistry
{
    private static readonly List<SavePlacedTileData> tiles = new();

    public static IReadOnlyList<SavePlacedTileData> Tiles => tiles;

    public static void Clear()
    {
        tiles.Clear();
    }

    public static void Register(ItemData item, Tilemap tilemap, Vector3Int cell)
    {
        if (item == null || tilemap == null)
            return;

        SavePlacedTileData existing = Find(tilemap.name, cell);
        if (existing == null)
        {
            existing = new SavePlacedTileData();
            tiles.Add(existing);
        }

        existing.itemId = SaveManager.GetItemId(item);
        existing.tilemapName = tilemap.name;
        existing.cell = SaveManager.ToSaveVector3Int(cell);
    }

    public static void Unregister(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
            return;

        for (int i = tiles.Count - 1; i >= 0; i--)
        {
            SavePlacedTileData data = tiles[i];
            if (data != null && data.tilemapName == tilemap.name && SaveManager.ToVector3Int(data.cell) == cell)
                tiles.RemoveAt(i);
        }
    }

    public static void Restore(IEnumerable<SavePlacedTileData> savedTiles)
    {
        tiles.Clear();
        if (savedTiles == null)
            return;

        foreach (SavePlacedTileData data in savedTiles)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.itemId) || string.IsNullOrWhiteSpace(data.tilemapName))
                continue;

            tiles.Add(data);
        }
    }

    private static SavePlacedTileData Find(string tilemapName, Vector3Int cell)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            SavePlacedTileData data = tiles[i];
            if (data != null && data.tilemapName == tilemapName && SaveManager.ToVector3Int(data.cell) == cell)
                return data;
        }

        return null;
    }
}
