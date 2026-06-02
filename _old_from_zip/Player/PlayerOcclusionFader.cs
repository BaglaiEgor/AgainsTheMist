using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerOcclusionFader : MonoBehaviour
{
#region Inspector
    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Optional. If empty, first Collider2D on player or children is used.")]
    [SerializeField] private Collider2D playerCollider;

    [Header("Detection")]
    [Tooltip("Only colliders on these layers can be faded.")]
    [SerializeField] private LayerMask occluderLayers = ~0;
    [Tooltip("Extra size added around player bounds when searching occluders.")]
    [SerializeField] private Vector2 boundsPadding = new Vector2(0.1f, 0.1f);
    [SerializeField] private bool includeTriggerColliders = false;
    [SerializeField] private bool requireInFrontOfPlayer = true;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField] private float fadedAlpha = 0.35f;
    [SerializeField] private float fadeSpeed = 10f;

    [Header("Gizmo")]
    [SerializeField] private bool showGizmoAlways = true;
    [SerializeField] private Color gizmoFillColor = new Color(0f, 1f, 1f, 0.2f);
    [SerializeField] private Color gizmoWireColor = new Color(0f, 1f, 1f, 0.9f);
#endregion

#region Runtime State
    private SpriteRenderer playerSpriteRenderer;
    private readonly Dictionary<int, FadableTarget> targets = new();
    private readonly HashSet<int> activeTargetIds = new();
    private readonly Dictionary<TileCellKey, TileCellFade> tileCellFades = new();
    private readonly HashSet<TileCellKey> activeTileCells = new();
    private readonly HashSet<int> processedTilemapIds = new();
#endregion

#region Unity Lifecycle
    void Awake()
    {
        ResolvePlayerReferences();
    }

    void OnDisable()
    {
        RestoreAllFades();
    }

    void LateUpdate()
    {
        ResolvePlayerReferences();
        activeTargetIds.Clear();
        activeTileCells.Clear();
        processedTilemapIds.Clear();

        if (!TryGetPlayerBounds(out Bounds playerBounds))
        {
            UpdateFades();
            return;
        }

        Bounds detectionBounds = BuildDetectionBounds(playerBounds);
        Collider2D[] hits = Physics2D.OverlapBoxAll(detectionBounds.center, detectionBounds.size, 0f, occluderLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (!includeTriggerColliders && hit.isTrigger)
                continue;

            if (player != null && hit.transform.IsChildOf(player))
                continue;

            // Skip objects that use their own fade logic.
            if (hit.GetComponentInParent<FadeWhenBehindPlayer>() != null)
                continue;

            Tilemap tilemap = hit.GetComponentInParent<Tilemap>();
            if (tilemap != null)
            {
                int tilemapId = tilemap.GetInstanceID();
                if (processedTilemapIds.Add(tilemapId))
                {
                    if (!requireInFrontOfPlayer || IsTilemapInFrontOfPlayer(tilemap))
                        CollectActiveTileCells(tilemap, detectionBounds);
                }

                continue;
            }

            Transform fadeRoot = ResolveFadeRoot(hit);
            if (fadeRoot == null)
                continue;

            FadableTarget target = GetOrCreateTarget(fadeRoot);
            if (!target.IsValid)
                continue;

            if (requireInFrontOfPlayer && !IsInFrontOfPlayer(target))
                continue;

            activeTargetIds.Add(target.Id);
        }

        UpdateFades();
    }

    void OnDrawGizmos()
    {
        if (!showGizmoAlways)
            return;

        DrawDetectionGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (showGizmoAlways)
            return;

        DrawDetectionGizmo();
    }
#endregion

#region Player and Bounds
    void ResolvePlayerReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player == null)
            return;

        if (playerCollider == null)
            playerCollider = player.GetComponentInChildren<Collider2D>(true);

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
    }

    bool TryGetPlayerBounds(out Bounds bounds)
    {
        bounds = default;
        if (player == null)
            return false;

        if (playerCollider != null)
        {
            bounds = playerCollider.bounds;
            return true;
        }

        if (playerSpriteRenderer != null)
        {
            bounds = playerSpriteRenderer.bounds;
            return true;
        }

        bounds = new Bounds(player.position, Vector3.one * 0.5f);
        return true;
    }

    Bounds BuildDetectionBounds(Bounds playerBounds)
    {
        Vector3 size = new Vector3(
            playerBounds.size.x + boundsPadding.x * 2f,
            playerBounds.size.y + boundsPadding.y * 2f,
            0.1f
        );

        return new Bounds(playerBounds.center, size);
    }

    void DrawDetectionGizmo()
    {
        ResolvePlayerReferences();

        if (!TryGetPlayerBounds(out Bounds playerBounds))
            return;

        Bounds detectionBounds = BuildDetectionBounds(playerBounds);
        Vector3 gizmoSize = detectionBounds.size;
        gizmoSize.z = 0f;

        Gizmos.color = gizmoFillColor;
        Gizmos.DrawCube(detectionBounds.center, gizmoSize);

        Gizmos.color = gizmoWireColor;
        Gizmos.DrawWireCube(detectionBounds.center, gizmoSize);
    }
#endregion

#region Tile Selection
    void CollectActiveTileCells(Tilemap tilemap, Bounds detectionBounds)
    {
        Vector3Int minCell = tilemap.WorldToCell(detectionBounds.min);
        Vector3Int maxCell = tilemap.WorldToCell(detectionBounds.max);

        int minX = Mathf.Min(minCell.x, maxCell.x);
        int maxX = Mathf.Max(minCell.x, maxCell.x);
        int minY = Mathf.Min(minCell.y, maxCell.y);
        int maxY = Mathf.Max(minCell.y, maxCell.y);
        int minZ = Mathf.Min(minCell.z, maxCell.z);
        int maxZ = Mathf.Max(minCell.z, maxCell.z);
        int tilemapId = tilemap.GetInstanceID();

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, z);
                    if (!tilemap.HasTile(cell))
                        continue;

                    if (tilemap.GetColliderType(cell) == Tile.ColliderType.None)
                        continue;

                    if (!DoesCellOverlapBounds(tilemap, cell, detectionBounds))
                        continue;

                    TileCellKey key = new TileCellKey(tilemapId, cell);
                    activeTileCells.Add(key);

                    if (!tileCellFades.ContainsKey(key))
                        tileCellFades[key] = new TileCellFade(tilemap, cell);
                }
            }
        }
    }

    bool DoesCellOverlapBounds(Tilemap tilemap, Vector3Int cell, Bounds detectionBounds)
    {
        Bounds cellBounds = GetCellWorldBounds(tilemap, cell);
        return cellBounds.Intersects(detectionBounds);
    }

    static Bounds GetCellWorldBounds(Tilemap tilemap, Vector3Int cell)
    {
        Vector3 center = tilemap.GetCellCenterWorld(cell);
        Vector3 baseSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : tilemap.cellSize;
        Vector3 scale = tilemap.transform.lossyScale;
        Vector3 size = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(baseSize.x * scale.x)),
            Mathf.Max(0.01f, Mathf.Abs(baseSize.y * scale.y)),
            0.1f
        );

        return new Bounds(center, size);
    }
#endregion

#region Target Resolution
    Transform ResolveFadeRoot(Collider2D hit)
    {
        SpriteRenderer sprite = hit.GetComponentInParent<SpriteRenderer>();
        if (sprite != null)
            return sprite.transform;

        return hit.transform;
    }

    FadableTarget GetOrCreateTarget(Transform root)
    {
        int id = root.GetInstanceID();
        if (targets.TryGetValue(id, out FadableTarget existing))
        {
            if (!existing.IsValid)
            {
                existing = new FadableTarget(root);
                targets[id] = existing;
            }

            return existing;
        }

        FadableTarget created = new FadableTarget(root);
        targets[id] = created;
        return created;
    }

    bool IsInFrontOfPlayer(FadableTarget target)
    {
        if (playerSpriteRenderer == null)
            return true;

        int playerLayerValue = SortingLayer.GetLayerValueFromID(playerSpriteRenderer.sortingLayerID);
        int playerOrder = playerSpriteRenderer.sortingOrder;

        if (!target.TryGetTopSorting(out int targetLayerValue, out int targetOrder))
            return true;

        if (targetLayerValue > playerLayerValue)
            return true;

        if (targetLayerValue < playerLayerValue)
            return false;

        return targetOrder >= playerOrder;
    }

    bool IsTilemapInFrontOfPlayer(Tilemap tilemap)
    {
        if (playerSpriteRenderer == null)
            return true;

        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null)
            return true;

        int playerLayerValue = SortingLayer.GetLayerValueFromID(playerSpriteRenderer.sortingLayerID);
        int playerOrder = playerSpriteRenderer.sortingOrder;
        int tileLayerValue = SortingLayer.GetLayerValueFromID(tilemapRenderer.sortingLayerID);
        int tileOrder = tilemapRenderer.sortingOrder;

        if (tileLayerValue > playerLayerValue)
            return true;

        if (tileLayerValue < playerLayerValue)
            return false;

        return tileOrder >= playerOrder;
    }
#endregion

#region Fade Application
    void UpdateFades()
    {
        List<int> deadTargetKeys = null;

        foreach (var pair in targets)
        {
            int id = pair.Key;
            FadableTarget target = pair.Value;

            if (!target.IsValid)
            {
                if (deadTargetKeys == null)
                    deadTargetKeys = new List<int>();

                deadTargetKeys.Add(id);
                continue;
            }

            float desiredMultiplier = activeTargetIds.Contains(id) ? fadedAlpha : 1f;
            target.Apply(desiredMultiplier, fadeSpeed, Time.deltaTime);
        }

        if (deadTargetKeys != null)
        {
            for (int i = 0; i < deadTargetKeys.Count; i++)
                targets.Remove(deadTargetKeys[i]);
        }

        List<TileCellKey> deadTileCellKeys = null;

        foreach (var pair in tileCellFades)
        {
            TileCellKey key = pair.Key;
            TileCellFade fade = pair.Value;
            bool isActive = activeTileCells.Contains(key);

            if (!fade.IsValid)
            {
                if (deadTileCellKeys == null)
                    deadTileCellKeys = new List<TileCellKey>();

                fade.TryRestore();
                deadTileCellKeys.Add(key);
                continue;
            }

            float desiredMultiplier = isActive ? fadedAlpha : 1f;
            fade.Apply(desiredMultiplier, fadeSpeed, Time.deltaTime);

            if (!isActive && fade.IsAtBaseAlpha())
            {
                if (deadTileCellKeys == null)
                    deadTileCellKeys = new List<TileCellKey>();

                fade.TryRestore();
                deadTileCellKeys.Add(key);
            }
        }

        if (deadTileCellKeys != null)
        {
            for (int i = 0; i < deadTileCellKeys.Count; i++)
                tileCellFades.Remove(deadTileCellKeys[i]);
        }
    }

    void RestoreAllFades()
    {
        foreach (var pair in targets)
            pair.Value.RestoreImmediate();

        foreach (var pair in tileCellFades)
            pair.Value.TryRestore();

        targets.Clear();
        activeTargetIds.Clear();
        tileCellFades.Clear();
        activeTileCells.Clear();
        processedTilemapIds.Clear();
    }
#endregion

#region Nested Types
    private class FadableTarget
    {
        public int Id { get; }
        public bool IsValid => root != null && hasAnyVisuals;

        private readonly Transform root;
        private readonly SpriteRenderer[] spriteRenderers;
        private readonly float[] spriteBaseAlphas;
        private readonly bool hasAnyVisuals;

        public FadableTarget(Transform root)
        {
            this.root = root;
            Id = root != null ? root.GetInstanceID() : 0;

            if (root == null)
            {
                spriteRenderers = new SpriteRenderer[0];
                spriteBaseAlphas = new float[0];
                hasAnyVisuals = false;
                return;
            }

            spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            spriteBaseAlphas = new float[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                spriteBaseAlphas[i] = spriteRenderers[i] != null ? spriteRenderers[i].color.a : 1f;

            hasAnyVisuals = spriteRenderers.Length > 0;
        }

        public bool TryGetTopSorting(out int layerValue, out int order)
        {
            bool found = false;
            layerValue = int.MinValue;
            order = int.MinValue;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                int lv = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
                int so = sr.sortingOrder;
                if (!found || lv > layerValue || (lv == layerValue && so > order))
                {
                    found = true;
                    layerValue = lv;
                    order = so;
                }
            }

            return found;
        }

        public void Apply(float alphaMultiplier, float fadeSpeed, float deltaTime)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                Color c = sr.color;
                float desiredAlpha = spriteBaseAlphas[i] * alphaMultiplier;
                c.a = Mathf.Lerp(c.a, desiredAlpha, deltaTime * fadeSpeed);
                sr.color = c;
            }
        }

        public void RestoreImmediate()
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                Color c = sr.color;
                c.a = spriteBaseAlphas[i];
                sr.color = c;
            }
        }
    }

    private readonly struct TileCellKey : System.IEquatable<TileCellKey>
    {
        public readonly int TilemapId;
        public readonly Vector3Int Cell;

        public TileCellKey(int tilemapId, Vector3Int cell)
        {
            TilemapId = tilemapId;
            Cell = cell;
        }

        public bool Equals(TileCellKey other)
        {
            return TilemapId == other.TilemapId && Cell == other.Cell;
        }

        public override bool Equals(object obj)
        {
            return obj is TileCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TilemapId * 486187739) ^ Cell.GetHashCode();
            }
        }
    }

    private class TileCellFade
    {
        public bool IsValid => tilemap != null && tilemap.HasTile(cell);

        private readonly Tilemap tilemap;
        private readonly Vector3Int cell;
        private readonly float baseAlpha;
        private readonly TileFlags originalFlags;
        private readonly bool unlockedColor;

        public TileCellFade(Tilemap tilemap, Vector3Int cell)
        {
            this.tilemap = tilemap;
            this.cell = cell;

            if (tilemap == null || !tilemap.HasTile(cell))
            {
                baseAlpha = 1f;
                originalFlags = TileFlags.None;
                unlockedColor = false;
                return;
            }

            baseAlpha = tilemap.GetColor(cell).a;
            originalFlags = tilemap.GetTileFlags(cell);

            if ((originalFlags & TileFlags.LockColor) != 0)
            {
                tilemap.SetTileFlags(cell, originalFlags & ~TileFlags.LockColor);
                unlockedColor = true;
            }
            else
            {
                unlockedColor = false;
            }
        }

        public void Apply(float alphaMultiplier, float fadeSpeed, float deltaTime)
        {
            if (!IsValid)
                return;

            Color color = tilemap.GetColor(cell);
            float desiredAlpha = baseAlpha * alphaMultiplier;
            color.a = Mathf.Lerp(color.a, desiredAlpha, deltaTime * fadeSpeed);
            tilemap.SetColor(cell, color);
        }

        public bool IsAtBaseAlpha(float tolerance = 0.01f)
        {
            if (!IsValid)
                return true;

            return Mathf.Abs(tilemap.GetColor(cell).a - baseAlpha) <= tolerance;
        }

        public void TryRestore()
        {
            if (tilemap == null || !tilemap.HasTile(cell))
                return;

            Color color = tilemap.GetColor(cell);
            color.a = baseAlpha;
            tilemap.SetColor(cell, color);

            if (unlockedColor)
                tilemap.SetTileFlags(cell, originalFlags);
        }
    }
#endregion
}
