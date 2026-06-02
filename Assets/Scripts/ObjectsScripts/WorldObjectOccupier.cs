using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldObjectOccupier : MonoBehaviour
{
    [Header("Footprint (cells)")]
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    [Header("Footprint Alignment")]
    [SerializeField] private bool centerOnTransform = false;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField, Range(0, 3)] private int placementRotationSteps;

    private readonly List<Vector3Int> occupiedCells = new();
    private readonly List<Vector3Int> gizmoCells = new();

    public bool CenterOnTransform => centerOnTransform;
    public int PlacementRotationSteps => placementRotationSteps;

    void OnEnable()
    {
        EnsureGroundTilemap();
        RegisterCells();
    }

    void OnDisable()
    {
        UnregisterCells();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
    }
#endif

    void EnsureGroundTilemap()
    {
        if (groundTilemap != null)
            return;

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.layer == groundLayer || tm.gameObject.name == "Ground")
            {
                groundTilemap = tm;
                break;
            }
        }

        if (groundTilemap == null)
            Debug.LogError("WorldObjectOccupier: не удалось найти Tilemap Ground (по имени или слою).");
    }

    void RegisterCells()
    {
        if (groundTilemap == null)
            return;

        UnregisterCells();
        Vector3Int anchorCell = groundTilemap.WorldToCell(transform.position);
        BuildFootprintCells(anchorCell, width, height, centerOnTransform, placementRotationSteps, occupiedCells);
        WorldGrid.RegisterObjectCells(occupiedCells);
    }

    void UnregisterCells()
    {
        WorldGrid.UnregisterObjectCells(occupiedCells);

        occupiedCells.Clear();
    }

    public static void BuildFootprintCells(
        Vector3Int anchorCell,
        int width,
        int height,
        bool centerOnTransform,
        List<Vector3Int> result
    )
    {
        BuildFootprintCells(anchorCell, width, height, centerOnTransform, 0, result);
    }

    public static void BuildFootprintCells(
        Vector3Int anchorCell,
        int width,
        int height,
        bool centerOnTransform,
        int rotationSteps,
        List<Vector3Int> result
    )
    {
        result.Clear();

        int safeWidth = Mathf.Max(1, width);
        int safeHeight = Mathf.Max(1, height);
        int normalizedRotation = NormalizeRotationSteps(rotationSteps);

        Vector3Int startCell = anchorCell;
        if (centerOnTransform)
            startCell -= new Vector3Int(safeWidth / 2, safeHeight / 2, 0);

        for (int x = 0; x < safeWidth; x++)
        {
            for (int y = 0; y < safeHeight; y++)
            {
                Vector3Int cell = startCell + new Vector3Int(x, y, 0);
                Vector3Int offset = cell - anchorCell;
                Vector3Int rotatedOffset = RotateOffset(offset, normalizedRotation);
                result.Add(anchorCell + rotatedOffset);
            }
        }
    }

    public void SetPlacementRotationSteps(int steps)
    {
        int normalized = NormalizeRotationSteps(steps);
        if (placementRotationSteps == normalized)
            return;

        placementRotationSteps = normalized;
        RegisterCells();
    }

    static int NormalizeRotationSteps(int steps)
    {
        int normalized = steps % 4;
        if (normalized < 0)
            normalized += 4;
        return normalized;
    }

    static Vector3Int RotateOffset(Vector3Int offset, int rotationSteps)
    {
        switch (rotationSteps)
        {
            case 1:
                return new Vector3Int(-offset.y, offset.x, 0);
            case 2:
                return new Vector3Int(-offset.x, -offset.y, 0);
            case 3:
                return new Vector3Int(offset.y, -offset.x, 0);
            default:
                return offset;
        }
    }

    void OnDrawGizmos()
    {
        DrawFootprintGizmo(new Color(1f, 1f, 0f, 0.25f));
    }

    void OnDrawGizmosSelected()
    {
        DrawFootprintGizmo(Color.yellow);
    }

    void DrawFootprintGizmo(Color color)
    {
        if (groundTilemap == null)
        {
            EnsureGroundTilemap();
            if (groundTilemap == null)
                return;
        }

        Gizmos.color = color;
        Vector3Int anchorCell = groundTilemap.WorldToCell(transform.position);
        BuildFootprintCells(anchorCell, width, height, centerOnTransform, placementRotationSteps, gizmoCells);

        foreach (var cell in gizmoCells)
        {
            Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);
            Vector3 size = groundTilemap.cellSize;
            Gizmos.DrawCube(worldPos, size);
        }
    }
}
