using UnityEngine;
using UnityEngine.Tilemaps;

public static class DoorPlacementUtility
{
    public static bool CanPlaceDoor(Tilemap targetTilemap, Tilemap buildTilemap, Tilemap decorTilemap, Vector3Int cell, TileBase markerTile, out Vector3Int firstWallDirection, out Vector3Int secondWallDirection)
    {
        firstWallDirection = Vector3Int.zero;
        secondWallDirection = Vector3Int.zero;

        if (targetTilemap == null || markerTile == null)
            return false;

        if (targetTilemap.HasTile(cell))
            return false;

        if (!WorldGrid.CanPlaceBuildTile(cell))
            return false;

        bool hasLeft = HasWallOrDoorMarker(cell + Vector3Int.left, markerTile, targetTilemap, buildTilemap, decorTilemap);
        bool hasRight = HasWallOrDoorMarker(cell + Vector3Int.right, markerTile, targetTilemap, buildTilemap, decorTilemap);
        if (hasLeft && hasRight)
        {
            firstWallDirection = Vector3Int.left;
            secondWallDirection = Vector3Int.right;
            return true;
        }

        bool hasUp = HasWallOrDoorMarker(cell + Vector3Int.up, markerTile, targetTilemap, buildTilemap, decorTilemap);
        bool hasDown = HasWallOrDoorMarker(cell + Vector3Int.down, markerTile, targetTilemap, buildTilemap, decorTilemap);
        if (hasUp && hasDown)
        {
            firstWallDirection = Vector3Int.up;
            secondWallDirection = Vector3Int.down;
            return true;
        }

        return false;
    }

    static bool HasWallOrDoorMarker(Vector3Int cell, TileBase markerTile, params Tilemap[] tilemaps)
    {
        if (tilemaps == null)
            return false;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap != null && IsWallOrDoorMarker(tilemap.GetTile(cell), markerTile))
                return true;
        }

        return false;
    }

    public static bool IsWallOrDoorMarker(TileBase tile, TileBase markerTile)
    {
        TileBase resolved = ResolveTile(tile);

        if (resolved == null)
            return false;

        if (resolved == markerTile)
            return true;

        WallRuleTile wall = resolved as WallRuleTile;
        return wall != null && wall.WallGroup == "Home";
    }

    static TileBase ResolveTile(TileBase tile)
    {
        if (tile is RuleOverrideTile overrideTile)
        {
            if (overrideTile.m_Tile != null)
                return overrideTile.m_Tile;

            if (overrideTile.m_InstanceTile != null)
                return overrideTile.m_InstanceTile;
        }

        return tile;
    }
}
