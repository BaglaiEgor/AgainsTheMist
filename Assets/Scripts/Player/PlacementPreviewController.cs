using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlacementPreviewController : MonoBehaviour
{
    private const string ToolHitColliderName = "HitBox";

    private enum PreviewMode
    {
        None,
        Placement,
        ToolTarget
    }

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap buildTilemap;
    [SerializeField] private Tilemap decorTilemap;
    [SerializeField] private Transform player;

    [Header("Visual")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Color validColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color toolTargetColor = new Color(1f, 1f, 1f, 0.65f);
    [SerializeField] private Color weakToolTargetColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField, Range(0.4f, 1f)] private float markerCellFill = 0.92f;
    [SerializeField] private int sortingOrder = 500;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private float zOffset = -0.01f;
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private float invalidFeedbackCooldown = 0.15f;
    [SerializeField] private float invalidFeedbackDuration = 0.12f;
    [SerializeField] private float invalidFeedbackStrength = 0.06f;

    private readonly List<Vector3Int> previewCells = new();
    private readonly List<PreviewMarker> markerPool = new();

    private static Sprite markerSprite;

    private ItemData currentPreviewItem;
    private Vector3Int currentAnchorCell;
    private bool previewActive;
    private PreviewMode previewMode;
    private int rotationSteps;

    private SpriteRenderer structurePreviewRenderer;
    private GameObject activeMarkerPrefab;
    private Tween invalidFeedbackTween;
    private float invalidFeedbackIntensity;
    private float nextInvalidFeedbackTime;
    private Vector2 invalidFeedbackDirection = Vector2.right;

    public int CurrentRotationSteps => rotationSteps;

    void Awake()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        invalidFeedbackTween?.Kill();
        invalidFeedbackIntensity = 0f;
        SetVisibleMarkerCount(0);
        SetStructurePreviewVisible(false);
    }

    void LateUpdate()
    {
        ResolveReferences();
        HandleRotationInput();

        if (!TryBuildPreview(out bool allValid))
        {
            SetVisibleMarkerCount(0);
            SetStructurePreviewVisible(false);
            return;
        }

        RenderPreview(GetPreviewColor(currentPreviewItem, allValid), allValid);
    }

    public void Configure(Inventory inventoryRef, Tilemap groundRef, Transform playerRef, InventoryUI inventoryUiRef = null, Tilemap buildRef = null, Tilemap decorRef = null)
    {
        if (inventoryRef != null)
            inventory = inventoryRef;

        if (inventoryUiRef != null)
            inventoryUI = inventoryUiRef;

        if (groundRef != null)
            groundTilemap = groundRef;

        if (buildRef != null)
            buildTilemap = buildRef;

        if (decorRef != null)
            decorTilemap = decorRef;

        if (playerRef != null)
            player = playerRef;
    }

    public void PlayInvalidFeedback()
    {
        if (!previewActive || Time.time < nextInvalidFeedbackTime)
            return;

        nextInvalidFeedbackTime = Time.time + Mathf.Max(0.01f, invalidFeedbackCooldown);
        invalidFeedbackTween?.Kill();

        invalidFeedbackDirection = Random.insideUnitCircle.normalized;
        if (invalidFeedbackDirection.sqrMagnitude <= 0.0001f)
            invalidFeedbackDirection = Vector2.right;

        invalidFeedbackIntensity = 1f;
        invalidFeedbackTween = DOTween
            .To(() => invalidFeedbackIntensity, value => invalidFeedbackIntensity = value, 0f, Mathf.Max(0.01f, invalidFeedbackDuration))
            .SetEase(Ease.OutQuad);
    }

    void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (inventoryUI == null)
            inventoryUI = GetComponent<InventoryUI>();

        if (inventoryUI == null)
            inventoryUI = Object.FindFirstObjectByType<InventoryUI>();

        if (player == null)
            player = transform;

        if (groundTilemap == null)
            TryFindGroundTilemap();

        if (buildTilemap == null)
            TryFindBuildTilemap();

        if (decorTilemap == null)
            TryFindDecorTilemap();
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

    void TryFindBuildTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int buildLayer = LayerMask.NameToLayer("Build");

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];

            if (tm.gameObject.layer == buildLayer || tm.gameObject.name == "Build" || tm.gameObject.name == "BuildTilemap")
            {
                buildTilemap = tm;
                break;
            }
        }
    }

    void TryFindDecorTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int decorLayer = LayerMask.NameToLayer("Decor");

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];

            if (tm.gameObject.layer == decorLayer || tm.gameObject.name == "Decor" || tm.gameObject.name == "DecorTilemap")
            {
                decorTilemap = tm;
                break;
            }
        }
    }

    bool TryBuildPreview(out bool allValid)
    {
        allValid = false;
        previewActive = false;
        previewMode = PreviewMode.None;
        currentPreviewItem = null;

        if ((inventory == null && inventoryUI == null) || groundTilemap == null || Camera.main == null || Mouse.current == null)
            return false;

        ItemData item = GetPreviewItem();
        if (CanPreviewPlacementItem(item))
            return TryBuildPlacementPreview(item, out allValid);

        if (CanPreviewToolTarget(item))
            return TryBuildToolTargetPreview(item, out allValid);

        return false;
    }

    bool TryBuildPlacementPreview(ItemData item, out bool allValid)
    {
        allValid = false;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (player != null && item.actionRadius > 0f)
        {
            if (Vector2.Distance(player.position, mouseWorld) > item.actionRadius)
                return false;
        }

        Vector3Int anchorCell = groundTilemap.WorldToCell(mouseWorld);
        BuildPreviewCells(item, anchorCell, rotationSteps, previewCells);

        if (previewCells.Count == 0)
            return false;

        currentPreviewItem = item;
        currentAnchorCell = anchorCell;
        previewActive = true;
        previewMode = PreviewMode.Placement;
        allValid = AreAllPreviewCellsValid(item, previewCells);
        return true;
    }

    bool TryBuildToolTargetPreview(ItemData item, out bool allValid)
    {
        allValid = false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (player != null && item.actionRadius > 0f)
        {
            if (Vector2.Distance(player.position, mouseWorld) > item.actionRadius)
                return false;
        }

        if (item.toolType == ToolType.Hoe || item.toolType == ToolType.Shovel)
        {
            currentPreviewItem = item;
            currentAnchorCell = groundTilemap.WorldToCell(mouseWorld);
            previewCells.Clear();
            previewCells.Add(currentAnchorCell);
            previewActive = true;
            previewMode = PreviewMode.ToolTarget;
            allValid = true;
            return true;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!IsValidToolHitCollider(hit))
                continue;

            WorldObject target = hit.GetComponentInParent<WorldObject>();
            if (target == null)
                continue;

            BuildToolTargetCells(target, previewCells);
            if (previewCells.Count == 0)
                return false;

            currentPreviewItem = item;
            currentAnchorCell = groundTilemap.WorldToCell(target.transform.position);
            previewActive = true;
            previewMode = PreviewMode.ToolTarget;
            allValid = CanToolMineObject(item, target);
            return true;
        }

        return false;
    }

    static bool CanPreviewPlacementItem(ItemData item)
    {
        if (item == null || item.type != ItemType.Structure)
            return false;

        if (item.placementMode == PlacementMode.Prefab)
            return item.prefab != null;

        if (item.placementMode == PlacementMode.Tile)
            return item.canPlaceOnWater ? item.waterTileToPlace != null : item.tileToPlace != null;

        return false;
    }

    static bool CanPreviewToolTarget(ItemData item)
    {
        return item != null && item.type == ItemType.Tool;
    }

    ItemData GetPreviewItem()
    {
        if (inventoryUI != null &&
            inventoryUI.TryGetCursorStack(out ItemData cursorItem, out _) &&
            cursorItem != null)
        {
            return cursorItem;
        }

        return inventory != null ? inventory.GetCurrentItem() : null;
    }

    static void BuildPreviewCells(ItemData item, Vector3Int anchorCell, int rotationSteps, List<Vector3Int> result)
    {
        result.Clear();

        if (item == null)
            return;

        if (item.placementMode == PlacementMode.Tile)
        {
            result.Add(anchorCell);
            return;
        }

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
                rotationSteps,
                result
            );

            return;
        }

        result.Add(anchorCell);
    }

    void BuildToolTargetCells(WorldObject target, List<Vector3Int> result)
    {
        result.Clear();

        if (target == null || groundTilemap == null)
            return;

        Vector3Int anchorCell = groundTilemap.WorldToCell(target.transform.position);
        WorldObjectOccupier occupier = target.GetComponent<WorldObjectOccupier>();
        if (occupier != null)
        {
            WorldObjectOccupier.BuildFootprintCells(
                anchorCell,
                occupier.width,
                occupier.height,
                occupier.CenterOnTransform,
                occupier.PlacementRotationSteps,
                result
            );

            return;
        }

        result.Add(anchorCell);
    }

    bool AreAllPreviewCellsValid(ItemData item, List<Vector3Int> cells)
    {
        if (item == null)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];

            if (item.placementMode == PlacementMode.Tile)
            {
                if (item.canPlaceOnWater)
                {
                    if (!WorldGrid.CanPlaceBridgeTile(cell))
                        return false;
                }
                else if (!WorldGrid.CanPlaceBuildTile(cell))
                {
                    return false;
                }
            }
            else if (item.isDoor)
            {
                if (!DoorPlacementUtility.CanPlaceDoor(buildTilemap, buildTilemap, decorTilemap, cell, item.doorWallMarkerTile, out _, out _))
                    return false;
            }
            else if (WorldGrid.GetObjectPlacementBlockReason(cell) != WorldGridBlockReason.None)
            {
                return false;
            }
        }

        return true;
    }

    Color GetPreviewColor(ItemData item, bool isValid)
    {
        if (previewMode == PreviewMode.ToolTarget)
            return isValid ? toolTargetColor : weakToolTargetColor;

        if (!isValid)
            return invalidColor;

        if (item != null && item.isDoor)
            return Color.green;

        return validColor;
    }

    void RenderPreview(Color color, bool isValid)
    {
        EnsureMarkerPool(previewCells.Count);
        SetVisibleMarkerCount(previewCells.Count);

        Vector3 markerScale = GetMarkerWorldScale();
        for (int i = 0; i < previewCells.Count; i++)
        {
            PreviewMarker marker = markerPool[i];
            Vector3 worldPos = groundTilemap.GetCellCenterWorld(previewCells[i]);
            worldPos.z += zOffset;

            marker.SetPosition(worldPos + GetInvalidFeedbackOffset(i));
            marker.SetScale(markerScale);
            marker.ApplyVisual(color, sortingOrder, sortingLayerName);
        }

        RenderStructureSpritePreview(color, isValid);
    }

    void HandleRotationInput()
    {
        if (!enableRotation || Keyboard.current == null)
            return;

        if (previewMode != PreviewMode.Placement)
            return;

        if (!previewActive && currentPreviewItem == null)
            return;

        if (!Keyboard.current.rKey.wasPressedThisFrame)
            return;

        if (currentPreviewItem != null && currentPreviewItem.isDoor)
            return;

        rotationSteps = (rotationSteps + 1) % 4;
    }

    static bool CanToolMineObject(ItemData item, WorldObject target)
    {
        if (item == null || target == null)
            return false;

        if (target.requiredTool == ToolType.None)
            return true;

        return target.requiredTool == item.toolType && item.toolPower >= target.minToolPower;
    }

    static bool IsValidToolHitCollider(Collider2D collider)
    {
        return collider != null && collider.gameObject.name == ToolHitColliderName;
    }

    void RenderStructureSpritePreview(Color color, bool isValid)
    {
        if (!previewActive || currentPreviewItem == null || currentPreviewItem.prefab == null)
        {
            SetStructurePreviewVisible(false);
            return;
        }

        EnsureStructurePreviewRenderer();
        if (structurePreviewRenderer == null)
            return;

        SpriteRenderer sourceRenderer = currentPreviewItem.prefab.GetComponentInChildren<SpriteRenderer>();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            SetStructurePreviewVisible(false);
            return;
        }

        structurePreviewRenderer.gameObject.SetActive(true);
        structurePreviewRenderer.sprite = sourceRenderer.sprite;
        structurePreviewRenderer.sortingOrder = sortingOrder - 1;
        structurePreviewRenderer.sortingLayerName = string.IsNullOrEmpty(sortingLayerName)
            ? sourceRenderer.sortingLayerName
            : sortingLayerName;
        structurePreviewRenderer.flipX = sourceRenderer.flipX;
        structurePreviewRenderer.flipY = sourceRenderer.flipY;
        structurePreviewRenderer.color = isValid
            ? new Color(validColor.r, validColor.g, validColor.b, 0.5f)
            : new Color(invalidColor.r, invalidColor.g, invalidColor.b, 0.5f);

        Vector3 worldPos = groundTilemap.GetCellCenterWorld(currentAnchorCell);
        worldPos += GetInvalidFeedbackOffset(0);
        worldPos.z += zOffset * 2f;

        Transform previewTransform = structurePreviewRenderer.transform;
        previewTransform.position = worldPos;
        previewTransform.rotation = Quaternion.Euler(0f, 0f, GetStructurePreviewRotationZ(currentPreviewItem, currentAnchorCell));
        previewTransform.localScale = sourceRenderer.transform.lossyScale;
    }

    float GetStructurePreviewRotationZ(ItemData item, Vector3Int anchorCell)
    {
        if (item != null && item.isDoor &&
            DoorPlacementUtility.CanPlaceDoor(buildTilemap, buildTilemap, decorTilemap, anchorCell, item.doorWallMarkerTile, out Vector3Int firstDirection, out Vector3Int secondDirection))
        {
            return IsHorizontalDoor(firstDirection, secondDirection) ? 0f : 90f;
        }

        return rotationSteps * 90f;
    }

    static bool IsHorizontalDoor(Vector3Int firstDirection, Vector3Int secondDirection)
    {
        return (firstDirection == Vector3Int.left && secondDirection == Vector3Int.right) ||
               (firstDirection == Vector3Int.right && secondDirection == Vector3Int.left);
    }

    void EnsureStructurePreviewRenderer()
    {
        if (structurePreviewRenderer != null)
            return;

        GameObject previewObject = new GameObject("PlacementPreviewStructure");
        previewObject.transform.SetParent(transform, false);
        structurePreviewRenderer = previewObject.AddComponent<SpriteRenderer>();
        previewObject.SetActive(false);
    }

    void SetStructurePreviewVisible(bool visible)
    {
        if (structurePreviewRenderer == null)
            return;

        structurePreviewRenderer.gameObject.SetActive(visible);
    }

    Vector3 GetMarkerWorldScale()
    {
        Vector3 baseSize = groundTilemap.layoutGrid != null ? groundTilemap.layoutGrid.cellSize : groundTilemap.cellSize;
        Vector3 lossyScale = groundTilemap.transform.lossyScale;

        float scaleX = Mathf.Max(0.01f, Mathf.Abs(baseSize.x * lossyScale.x) * markerCellFill);
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(baseSize.y * lossyScale.y) * markerCellFill);
        return new Vector3(scaleX, scaleY, 1f);
    }

    Vector3 GetInvalidFeedbackOffset(int index)
    {
        if (invalidFeedbackIntensity <= 0.001f)
            return Vector3.zero;

        float phase = index % 2 == 0 ? 1f : -1f;
        Vector2 offset = invalidFeedbackDirection * (invalidFeedbackStrength * invalidFeedbackIntensity * phase);
        return new Vector3(offset.x, offset.y, 0f);
    }

    void EnsureMarkerPool(int requiredCount)
    {
        if (activeMarkerPrefab != markerPrefab)
        {
            ClearMarkerPool();
            activeMarkerPrefab = markerPrefab;
        }

        while (markerPool.Count < requiredCount)
        {
            GameObject markerObject = new GameObject($"PlacementPreviewCell_{markerPool.Count}");
            markerObject.transform.SetParent(transform, false);

            SpriteRenderer sr = null;
            if (markerPrefab != null)
            {
                GameObject prefabInstance = Instantiate(markerPrefab, markerObject.transform);
                prefabInstance.transform.localPosition = Vector3.zero;
                prefabInstance.transform.localRotation = Quaternion.identity;
                prefabInstance.transform.localScale = Vector3.one;
            }
            else
            {
                sr = markerObject.AddComponent<SpriteRenderer>();
                sr.sprite = GetMarkerSprite();
                sr.sortingOrder = sortingOrder;

                if (!string.IsNullOrEmpty(sortingLayerName))
                    sr.sortingLayerName = sortingLayerName;
            }

            markerObject.SetActive(false);
            PreviewMarker marker = new PreviewMarker(markerObject, sr);
            if (!marker.HasRenderers)
            {
                sr = markerObject.AddComponent<SpriteRenderer>();
                sr.sprite = GetMarkerSprite();
                marker = new PreviewMarker(markerObject, sr);
            }

            markerPool.Add(marker);
        }
    }

    void ClearMarkerPool()
    {
        for (int i = 0; i < markerPool.Count; i++)
            markerPool[i].Destroy();

        markerPool.Clear();
    }

    void SetVisibleMarkerCount(int visibleCount)
    {
        for (int i = 0; i < markerPool.Count; i++)
            markerPool[i].SetVisible(i < visibleCount);
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

    private class PreviewMarker
    {
        private readonly GameObject root;
        private readonly SpriteRenderer[] renderers;

        public bool HasRenderers => renderers != null && renderers.Length > 0;

        public PreviewMarker(GameObject rootObject, SpriteRenderer fallbackRenderer)
        {
            root = rootObject;
            renderers = rootObject.GetComponentsInChildren<SpriteRenderer>(true);

            if ((renderers == null || renderers.Length == 0) && fallbackRenderer != null)
                renderers = new[] { fallbackRenderer };
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void SetPosition(Vector3 position)
        {
            if (root != null)
                root.transform.position = position;
        }

        public void SetScale(Vector3 scale)
        {
            if (root != null)
                root.transform.localScale = scale;
        }

        public void ApplyVisual(Color color, int sortingOrder, string sortingLayerName)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                    continue;

                sr.color = color;
                sr.sortingOrder = sortingOrder;

                if (!string.IsNullOrEmpty(sortingLayerName))
                    sr.sortingLayerName = sortingLayerName;
            }
        }

        public void Destroy()
        {
            if (root != null)
                Object.Destroy(root);
        }
    }
}
