using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum WorldGridBlockReason
{
    None,
    MissingGroundTile,
    Water,
    Obstacle,
    BuildTile,
    OccupiedByObject
}

public static class WorldGrid
{
    private static readonly HashSet<Vector3Int> objectOccupiedCells = new();
    private static readonly HashSet<Vector3Int> placedTileCells = new();

    private static Tilemap groundTilemap;
    private static Tilemap waterTilemap;
    private static Tilemap obstacleTilemap;
    private static Tilemap buildTilemap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        objectOccupiedCells.Clear();
        placedTileCells.Clear();

        groundTilemap = null;
        waterTilemap = null;
        obstacleTilemap = null;
        buildTilemap = null;
    }

    public static void ConfigureTilemaps(
        Tilemap ground = null,
        Tilemap water = null,
        Tilemap obstacle = null,
        Tilemap build = null
    )
    {
        if (ground != null)
            groundTilemap = ground;

        if (water != null)
            waterTilemap = water;

        if (obstacle != null)
            obstacleTilemap = obstacle;

        if (build != null)
            buildTilemap = build;
    }

    public static bool HasGround(Vector3Int cell)
    {
        return groundTilemap != null && groundTilemap.HasTile(cell);
    }

    public static bool IsOccupied(Vector3Int cell)
    {
        return objectOccupiedCells.Contains(cell);
    }

    public static bool HasPlacedBuildTile(Vector3Int cell)
    {
        if (buildTilemap != null && buildTilemap.HasTile(cell))
            return true;

        return placedTileCells.Contains(cell);
    }

    public static TileBase GetTile(Vector3Int cell, Tilemap tilemapOverride)
    {
        if (tilemapOverride == null)
            return null;

        return tilemapOverride.GetTile(cell);
    }

    public static bool RemoveTile(Vector3Int cell, Tilemap tilemapOverride)
    {
        if (tilemapOverride == null || !tilemapOverride.HasTile(cell))
            return false;

        tilemapOverride.SetTile(cell, null);
        if (tilemapOverride == buildTilemap)
            placedTileCells.Remove(cell);

        return true;
    }

    public static WorldGridBlockReason GetObjectPlacementBlockReason(Vector3Int cell)
    {
        if (!HasGround(cell))
            return WorldGridBlockReason.MissingGroundTile;

        if (waterTilemap != null && waterTilemap.HasTile(cell))
            return WorldGridBlockReason.Water;

        if (obstacleTilemap != null && obstacleTilemap.HasTile(cell))
            return WorldGridBlockReason.Obstacle;

        if (objectOccupiedCells.Contains(cell))
            return WorldGridBlockReason.OccupiedByObject;

        return WorldGridBlockReason.None;
    }

    public static WorldGridBlockReason GetBuildTilePlacementBlockReason(Vector3Int cell)
    {
        // Build tiles (paths/decor) are checked only against build-tile occupancy.
        // Ground is optional here: if no ground tilemap is configured, we do not block placement.
        if (groundTilemap != null && !groundTilemap.HasTile(cell))
            return WorldGridBlockReason.MissingGroundTile;

        if (HasPlacedBuildTile(cell))
            return WorldGridBlockReason.BuildTile;

        return WorldGridBlockReason.None;
    }

    public static bool CanPlaceObject(Vector3Int cell)
    {
        return GetObjectPlacementBlockReason(cell) == WorldGridBlockReason.None;
    }

    public static bool CanPlaceObject(IReadOnlyList<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (!CanPlaceObject(cells[i]))
                return false;
        }

        return true;
    }

    public static bool CanPlaceBuildTile(Vector3Int cell)
    {
        return GetBuildTilePlacementBlockReason(cell) == WorldGridBlockReason.None;
    }

    public static bool CanSpawn(Vector3Int cell)
    {
        if (!CanPlaceObject(cell))
            return false;

        // Prevent natural respawn on top of player-placed build tiles.
        return !HasPlacedBuildTile(cell);
    }

    public static void RegisterObjectCell(Vector3Int cell)
    {
        objectOccupiedCells.Add(cell);
    }

    public static void RegisterObjectCells(IEnumerable<Vector3Int> cells)
    {
        if (cells == null)
            return;

        foreach (var cell in cells)
            objectOccupiedCells.Add(cell);
    }

    public static void UnregisterObjectCell(Vector3Int cell)
    {
        objectOccupiedCells.Remove(cell);
    }

    public static void UnregisterObjectCells(IEnumerable<Vector3Int> cells)
    {
        if (cells == null)
            return;

        foreach (var cell in cells)
            objectOccupiedCells.Remove(cell);
    }

    public static void RegisterPlacedTile(Vector3Int cell)
    {
        placedTileCells.Add(cell);
    }

    public static void UnregisterPlacedTile(Vector3Int cell)
    {
        placedTileCells.Remove(cell);
    }

    // Legacy wrappers for old call sites. Object occupancy only.
    public static void Register(Vector3Int cell)
    {
        RegisterObjectCell(cell);
    }

    // Legacy wrappers for old call sites. Object occupancy only.
    public static void Unregister(Vector3Int cell)
    {
        UnregisterObjectCell(cell);
    }
}
