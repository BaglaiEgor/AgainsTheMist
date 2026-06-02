using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class BridgeGapPatchManager
{
    private const float PixelsPerCell = 16f;
    private const float SideGapPixels = 3f;
    private const float VerticalGapPixels = 7f;
    private const float PatchLengthPixels = 10f;
    private const float PatchOverlapPixels = 1f;

    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    private static readonly Dictionary<Vector3Int, List<GameObject>> patchesByBridgeCell = new();
    private static Transform patchRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        patchesByBridgeCell.Clear();
        patchRoot = null;
    }

    public static void RebuildForTilemap(Tilemap bridgeTilemap)
    {
        if (bridgeTilemap == null)
            return;

        BoundsInt bounds = bridgeTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (bridgeTilemap.HasTile(cell))
                CreatePatches(cell, bridgeTilemap);
        }

        RefreshColliderGeometry(bridgeTilemap);
    }

    public static void CreatePatches(Vector3Int bridgeCell, Tilemap bridgeTilemap)
    {
        RemovePatches(bridgeCell);
        if (bridgeTilemap == null)
            return;

        List<GameObject> patches = null;
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector3Int direction = Directions[i];
            Vector3Int neighbor = bridgeCell + direction;
            if (!WorldGrid.HasGround(neighbor))
                continue;

            GameObject patch = CreatePatchObject(bridgeCell, direction, bridgeTilemap);
            if (patch == null)
                continue;

            patches ??= new List<GameObject>();
            patches.Add(patch);
        }

        if (patches != null)
            patchesByBridgeCell[bridgeCell] = patches;
    }

    public static void RemovePatches(Vector3Int bridgeCell)
    {
        if (!patchesByBridgeCell.TryGetValue(bridgeCell, out List<GameObject> patches))
            return;

        for (int i = 0; i < patches.Count; i++)
        {
            GameObject patch = patches[i];
            if (patch == null)
                continue;

            Collider2D[] colliders = patch.GetComponents<Collider2D>();
            for (int j = 0; j < colliders.Length; j++)
                colliders[j].enabled = false;

            if (Application.isPlaying)
                Object.Destroy(patch);
            else
                Object.DestroyImmediate(patch);
        }

        patchesByBridgeCell.Remove(bridgeCell);
    }

    public static void RefreshColliderGeometry(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        tilemap.RefreshAllTiles();

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
            return;

        tilemapCollider.ProcessTilemapChanges();
        RefreshCompositeGeometry(tilemapCollider.attachedRigidbody);
        RefreshCompositeGeometry(tilemapCollider.GetComponentInParent<Rigidbody2D>());
    }

    static void RefreshCompositeGeometry(Rigidbody2D body)
    {
        if (body == null)
            return;

        CompositeCollider2D[] composites = body.GetComponents<CompositeCollider2D>();
        for (int i = 0; i < composites.Length; i++)
        {
            if (composites[i] != null)
                composites[i].GenerateGeometry();
        }
    }

    static GameObject CreatePatchObject(Vector3Int bridgeCell, Vector3Int direction, Tilemap bridgeTilemap)
    {
        Vector3 bridgeCenter = bridgeTilemap.GetCellCenterWorld(bridgeCell);
        Vector2 cellSize = GetWorldCellSize(bridgeTilemap);
        if (cellSize.x <= 0f || cellSize.y <= 0f)
            return null;

        float pixelX = cellSize.x / PixelsPerCell;
        float pixelY = cellSize.y / PixelsPerCell;
        Vector3 patchCenter = bridgeCenter;
        Vector2 patchSize;

        if (direction.x != 0)
        {
            float gap = SideGapPixels * pixelX;
            float thickness = (SideGapPixels + PatchOverlapPixels * 2f) * pixelX;
            float length = PatchLengthPixels * pixelY;
            float edgeX = bridgeCenter.x + direction.x * cellSize.x * 0.5f;

            patchCenter.x = edgeX + direction.x * gap * 0.5f;
            patchSize = new Vector2(thickness, length);
        }
        else
        {
            float gap = VerticalGapPixels * pixelY;
            float thickness = (VerticalGapPixels + PatchOverlapPixels * 2f) * pixelY;
            float length = PatchLengthPixels * pixelX;
            float edgeY = bridgeCenter.y + direction.y * cellSize.y * 0.5f;

            patchCenter.y = edgeY + direction.y * gap * 0.5f;
            patchSize = new Vector2(length, thickness);
        }

        Transform root = GetPatchRoot(bridgeTilemap);
        GameObject patchObject = new GameObject($"BridgeGapPatch_{bridgeCell.x}_{bridgeCell.y}_{DirectionName(direction)}");
        patchObject.layer = bridgeTilemap.gameObject.layer;
        patchObject.transform.SetParent(root, true);
        patchObject.transform.position = patchCenter;

        BoxCollider2D collider = patchObject.AddComponent<BoxCollider2D>();
        collider.size = patchSize;
        collider.isTrigger = false;
        collider.compositeOperation = Collider2D.CompositeOperation.Merge;
        return patchObject;
    }

    static Transform GetPatchRoot(Tilemap bridgeTilemap)
    {
        if (patchRoot != null)
            return patchRoot;

        Transform compositeRoot = GetCompositeRoot(bridgeTilemap);
        GameObject rootObject = new GameObject("BridgeGapPatches");
        rootObject.layer = bridgeTilemap != null ? bridgeTilemap.gameObject.layer : 0;
        patchRoot = rootObject.transform;
        if (compositeRoot != null)
            patchRoot.SetParent(compositeRoot, false);

        return patchRoot;
    }

    static Transform GetCompositeRoot(Tilemap bridgeTilemap)
    {
        if (bridgeTilemap == null)
            return null;

        Rigidbody2D body = bridgeTilemap.GetComponentInParent<Rigidbody2D>();
        if (body != null)
            return body.transform;

        return bridgeTilemap.transform.parent;
    }

    static Vector2 GetWorldCellSize(Tilemap tilemap)
    {
        Grid grid = tilemap.layoutGrid;
        Vector3 cellSize = grid != null ? grid.cellSize : tilemap.cellSize;
        Vector3 scale = tilemap.transform.lossyScale;
        return new Vector2(Mathf.Abs(cellSize.x * scale.x), Mathf.Abs(cellSize.y * scale.y));
    }

    static string DirectionName(Vector3Int direction)
    {
        if (direction == Vector3Int.up)
            return "Up";
        if (direction == Vector3Int.down)
            return "Down";
        if (direction == Vector3Int.left)
            return "Left";
        return "Right";
    }
}
