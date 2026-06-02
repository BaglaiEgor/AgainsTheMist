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

    private readonly List<Vector3Int> occupiedCells = new();
    private readonly List<Vector3Int> gizmoCells = new();

    public bool CenterOnTransform => centerOnTransform;

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
        BuildFootprintCells(anchorCell, width, height, centerOnTransform, occupiedCells);
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
        result.Clear();

        int safeWidth = Mathf.Max(1, width);
        int safeHeight = Mathf.Max(1, height);

        Vector3Int startCell = anchorCell;
        if (centerOnTransform)
            startCell -= new Vector3Int(safeWidth / 2, safeHeight / 2, 0);

        for (int x = 0; x < safeWidth; x++)
        {
            for (int y = 0; y < safeHeight; y++)
                result.Add(startCell + new Vector3Int(x, y, 0));
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
        BuildFootprintCells(anchorCell, width, height, centerOnTransform, gizmoCells);

        foreach (var cell in gizmoCells)
        {
            Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);
            Vector3 size = groundTilemap.cellSize;
            Gizmos.DrawCube(worldPos, size);
        }
    }
}
