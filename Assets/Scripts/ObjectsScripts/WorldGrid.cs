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
    DecorTile,
    OccupiedByObject
}

public static class WorldGrid
{
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    private static readonly HashSet<Vector3Int> objectOccupiedCells = new();
    private static readonly HashSet<Vector3Int> placedTileCells = new();
    private static readonly HashSet<Vector3Int> placedBridgeCells = new();

    private static Tilemap groundTilemap;
    private static Tilemap snowTilemap;
    private static Tilemap upperSnowTilemap;
    private static Tilemap waterTilemap;
    private static Tilemap obstacleTilemap;
    private static Tilemap buildTilemap;
    private static Tilemap decorTilemap;
    private static Tilemap pathTilemap;
    private static Tilemap bridgeTilemap;
    private static Tilemap snowBridgeTilemap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        objectOccupiedCells.Clear();
        placedTileCells.Clear();
        placedBridgeCells.Clear();

        groundTilemap = null;
        snowTilemap = null;
        upperSnowTilemap = null;
        waterTilemap = null;
        obstacleTilemap = null;
        buildTilemap = null;
        decorTilemap = null;
        pathTilemap = null;
        bridgeTilemap = null;
        snowBridgeTilemap = null;
    }

    public static void ConfigureTilemaps(
        Tilemap ground = null,
        Tilemap water = null,
        Tilemap obstacle = null,
        Tilemap build = null,
        Tilemap snow = null,
        Tilemap bridge = null,
        Tilemap snowBridge = null,
        Tilemap decor = null,
        Tilemap path = null,
        Tilemap upperSnow = null
    )
    {
        if (ground != null)
            groundTilemap = ground;

        if (snow != null)
            snowTilemap = snow;

        if (upperSnow != null)
            upperSnowTilemap = upperSnow;

        if (water != null)
            waterTilemap = water;

        if (obstacle != null)
            obstacleTilemap = obstacle;

        if (build != null)
            buildTilemap = build;

        if (decor != null)
            decorTilemap = decor;

        if (path != null)
            pathTilemap = path;

        if (bridge != null)
            bridgeTilemap = bridge;

        if (snowBridge != null)
            snowBridgeTilemap = snowBridge;
    }

    public static bool HasGround(Vector3Int cell)
    {
        return (groundTilemap != null && groundTilemap.HasTile(cell)) ||
               HasSnow(cell);
    }

    public static bool HasSnow(Vector3Int cell)
    {
        return (snowTilemap != null && snowTilemap.HasTile(cell)) ||
               HasUpperSnow(cell);
    }

    public static bool HasUpperSnow(Vector3Int cell)
    {
        return upperSnowTilemap != null && upperSnowTilemap.HasTile(cell);
    }

    public static bool HasWater(Vector3Int cell)
    {
        return waterTilemap != null && waterTilemap.HasTile(cell);
    }

    public static bool HasPlacedBridge(Vector3Int cell)
    {
        return HasBridgeTile(cell);
    }

    public static bool HasBridgeTile(Vector3Int cell)
    {
        return placedBridgeCells.Contains(cell) ||
               (bridgeTilemap != null && bridgeTilemap.HasTile(cell)) ||
               (snowBridgeTilemap != null && snowBridgeTilemap.HasTile(cell));
    }

    public static bool TryGetGroundCell(Vector3 worldPosition, out Vector3Int cell)
    {
        cell = default;
        Tilemap cellTilemap = groundTilemap != null ? groundTilemap : snowTilemap != null ? snowTilemap : upperSnowTilemap;
        if (cellTilemap == null)
            return false;

        cell = cellTilemap.WorldToCell(worldPosition);
        return true;
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

    public static bool HasDecorTile(Vector3Int cell)
    {
        return decorTilemap != null && decorTilemap.HasTile(cell);
    }

    public static bool HasPathTile(Vector3Int cell)
    {
        return pathTilemap != null && pathTilemap.HasTile(cell);
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

        bool isBridgeTilemap = tilemapOverride == bridgeTilemap || tilemapOverride == snowBridgeTilemap;
        tilemapOverride.SetTile(cell, null);
        if (tilemapOverride == buildTilemap)
            placedTileCells.Remove(cell);

        if (isBridgeTilemap)
        {
            placedBridgeCells.Remove(cell);
            placedTileCells.Remove(cell);
            BridgeGapPatchManager.RemovePatches(cell);
            BridgeGapPatchManager.RefreshColliderGeometry(tilemapOverride);
        }

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

        if (HasPlacedBuildTile(cell))
            return WorldGridBlockReason.BuildTile;

        if (objectOccupiedCells.Contains(cell))
            return WorldGridBlockReason.OccupiedByObject;

        return WorldGridBlockReason.None;
    }

    public static WorldGridBlockReason GetBuildTilePlacementBlockReason(Vector3Int cell)
    {
        // Ground is optional here: if no ground tilemap is configured, we do not block placement.
        if (HasAnyGroundTilemap() && !HasGround(cell))
            return WorldGridBlockReason.MissingGroundTile;

        if (HasWater(cell))
            return WorldGridBlockReason.Water;

        if (obstacleTilemap != null && obstacleTilemap.HasTile(cell))
            return WorldGridBlockReason.Obstacle;

        if (HasPlacedBuildTile(cell))
            return WorldGridBlockReason.BuildTile;

        if (objectOccupiedCells.Contains(cell))
            return WorldGridBlockReason.OccupiedByObject;

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

    public static bool CanPlaceBridgeTile(Vector3Int cell)
    {
        if (!HasWater(cell))
            return false;

        if (HasGround(cell))
            return false;

        if (obstacleTilemap != null && obstacleTilemap.HasTile(cell))
            return false;

        if (objectOccupiedCells.Contains(cell))
            return false;

        if (HasPlacedBuildTile(cell) || HasBridgeTile(cell))
            return false;

        return HasBridgeAnchorNeighbor(cell);
    }

    public static bool HasBridgeAnchorNeighbor(Vector3Int cell)
    {
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector3Int neighbor = cell + CardinalDirections[i];
            if (HasGround(neighbor) || HasBridgeTile(neighbor))
                return true;
        }

        return false;
    }

    public static bool CanSpawn(Vector3Int cell)
    {
        if (!CanPlaceObject(cell))
            return false;

        return !HasDecorTile(cell) && !HasPathTile(cell);
    }

    static bool HasAnyGroundTilemap()
    {
        return groundTilemap != null || snowTilemap != null || upperSnowTilemap != null;
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

    public static void RegisterPlacedBridge(Vector3Int cell)
    {
        placedBridgeCells.Add(cell);
        placedTileCells.Add(cell);
    }

    public static void UnregisterPlacedBridge(Vector3Int cell)
    {
        placedBridgeCells.Remove(cell);
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
