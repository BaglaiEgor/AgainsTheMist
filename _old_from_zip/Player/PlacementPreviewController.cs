using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlacementPreviewController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform player;

    [Header("Visual")]
    [SerializeField] private Color validColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField, Range(0.4f, 1f)] private float markerCellFill = 0.92f;
    [SerializeField] private int sortingOrder = 500;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private float zOffset = -0.01f;

    private readonly List<Vector3Int> previewCells = new();
    private readonly List<SpriteRenderer> markerPool = new();

    private static Sprite markerSprite;

    void Awake()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        SetVisibleMarkerCount(0);
    }

    void LateUpdate()
    {
        ResolveReferences();

        if (!TryBuildPreview(out bool allValid))
        {
            SetVisibleMarkerCount(0);
            return;
        }

        RenderPreview(allValid ? validColor : invalidColor);
    }

    public void Configure(Inventory inventoryRef, Tilemap groundRef, Transform playerRef)
    {
        if (inventoryRef != null)
            inventory = inventoryRef;

        if (groundRef != null)
            groundTilemap = groundRef;

        if (playerRef != null)
            player = playerRef;
    }

    void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (player == null)
            player = transform;

        if (groundTilemap == null)
            TryFindGroundTilemap();
    }

    void TryFindGroundTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];

            if (groundTilemap == null &&
                (tm.gameObject.layer == groundLayer || tm.gameObject.name == "Ground"))
            {
                groundTilemap = tm;
            }

            if (groundTilemap != null)
                break;
        }
    }

    bool TryBuildPreview(out bool allValid)
    {
        allValid = false;

        if (inventory == null || groundTilemap == null || Camera.main == null || Mouse.current == null)
            return false;

        ItemData item = inventory.GetCurrentItem();
        if (item == null || item.type != ItemType.Structure || item.placementMode != PlacementMode.Prefab)
            return false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (player != null && item.actionRadius > 0f)
        {
            if (Vector2.Distance(player.position, mouseWorld) > item.actionRadius)
                return false;
        }

        Vector3Int anchorCell = groundTilemap.WorldToCell(mouseWorld);
        BuildPreviewCells(item, anchorCell, previewCells);

        if (previewCells.Count == 0)
            return false;

        allValid = AreAllPreviewCellsValid(previewCells);
        return true;
    }

    static void BuildPreviewCells(ItemData item, Vector3Int anchorCell, List<Vector3Int> result)
    {
        result.Clear();

        if (item == null)
            return;

        if (item.prefab == null)
            return;

        WorldObjectOccupier occupier = item.prefab.GetComponent<WorldObjectOccupier>();
        if (occupier != null)
        {
            WorldObjectOccupier.BuildFootprintCells(
                anchorCell,
                occupier.width,
                occupier.height,
                occupier.CenterOnTransform,
                result
            );

            return;
        }

        result.Add(anchorCell);
    }

    static bool AreAllPreviewCellsValid(List<Vector3Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];

            if (WorldGrid.GetObjectPlacementBlockReason(cell) != WorldGridBlockReason.None)
                return false;
        }

        return true;
    }

    void RenderPreview(Color color)
    {
        EnsureMarkerPool(previewCells.Count);
        SetVisibleMarkerCount(previewCells.Count);

        Vector3 markerScale = GetMarkerWorldScale();
        for (int i = 0; i < previewCells.Count; i++)
        {
            SpriteRenderer marker = markerPool[i];
            Vector3 worldPos = groundTilemap.GetCellCenterWorld(previewCells[i]);
            worldPos.z += zOffset;

            marker.transform.position = worldPos;
            marker.transform.localScale = markerScale;
            marker.color = color;
            marker.sortingOrder = sortingOrder;

            if (!string.IsNullOrEmpty(sortingLayerName))
                marker.sortingLayerName = sortingLayerName;
        }
    }

    Vector3 GetMarkerWorldScale()
    {
        Vector3 baseSize = groundTilemap.layoutGrid != null ? groundTilemap.layoutGrid.cellSize : groundTilemap.cellSize;
        Vector3 lossyScale = groundTilemap.transform.lossyScale;

        float scaleX = Mathf.Max(0.01f, Mathf.Abs(baseSize.x * lossyScale.x) * markerCellFill);
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(baseSize.y * lossyScale.y) * markerCellFill);
        return new Vector3(scaleX, scaleY, 1f);
    }

    void EnsureMarkerPool(int requiredCount)
    {
        while (markerPool.Count < requiredCount)
        {
            GameObject markerObject = new GameObject($"PlacementPreviewCell_{markerPool.Count}");
            markerObject.transform.SetParent(transform, false);

            SpriteRenderer sr = markerObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetMarkerSprite();
            sr.sortingOrder = sortingOrder;

            if (!string.IsNullOrEmpty(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;

            markerObject.SetActive(false);
            markerPool.Add(sr);
        }
    }

    void SetVisibleMarkerCount(int visibleCount)
    {
        for (int i = 0; i < markerPool.Count; i++)
            markerPool[i].gameObject.SetActive(i < visibleCount);
    }

    static Sprite GetMarkerSprite()
    {
        if (markerSprite != null)
            return markerSprite;

        const int size = 32;
        const int border = 3;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 opaque = new Color32(255, 255, 255, 255);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                pixels[y * size + x] = isBorder ? opaque : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        markerSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        markerSprite.name = "PlacementPreviewMarkerSprite";
        return markerSprite;
    }
}
