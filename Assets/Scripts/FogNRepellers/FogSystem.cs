using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[ExecuteAlways]
public class FogSystem : MonoBehaviour
{
    private static FogSystem instance;

    [Header("Enemy Fog")]
    [SerializeField] private FogMaskDrawer fogMaskDrawer;
    [Min(0f)] [SerializeField] private float enemySoftEdgeFallbackPadding = 0.25f;

    private readonly List<FogRepeller> activeRepellers = new List<FogRepeller>();

    public static FogSystem Instance
    {
        get
        {
            if (instance != null)
                return instance;

            if (TryGetExistingInstance(out FogSystem existing))
                return existing;

            return null;
        }
    }

    public static bool TryGetExistingInstance(out FogSystem fogSystem)
    {
        if (instance != null)
        {
            fogSystem = instance;
            return true;
        }

        fogSystem = FindFirstObjectByType<FogSystem>(FindObjectsInactive.Include);
        if (fogSystem != null)
            instance = fogSystem;

        return fogSystem != null;
    }

    public IReadOnlyList<FogRepeller> ActiveRepellers => activeRepellers;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            RebuildRepellersFromScene();
            return;
        }

        if (instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                    DestroyImmediate(gameObject);
            };
#endif
        }
    }

    void Start()
    {
        if (Application.isPlaying)
            EnsureSceneObjectTints();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void RegisterRepeller(FogRepeller repeller)
    {
        if (repeller == null)
            return;

        if (!activeRepellers.Contains(repeller))
            activeRepellers.Add(repeller);
    }

    public void UnregisterRepeller(FogRepeller repeller)
    {
        if (repeller == null)
            return;

        activeRepellers.Remove(repeller);
    }

    public void RebuildRepellersFromScene()
    {
        activeRepellers.Clear();
        FogRepeller[] repellersInScene = FindObjectsByType<FogRepeller>(FindObjectsSortMode.None);
        for (int i = 0; i < repellersInScene.Length; i++)
        {
            FogRepeller repeller = repellersInScene[i];
            if (repeller != null)
                activeRepellers.Add(repeller);
        }
    }

    void EnsureSceneObjectTints()
    {
        WorldObjectOccupier[] occupiers = FindObjectsByType<WorldObjectOccupier>(FindObjectsSortMode.None);
        for (int i = 0; i < occupiers.Length; i++)
        {
            if (occupiers[i] != null)
                FogObjectTint.EnsureOn(occupiers[i].gameObject);
        }
    }

    public float GetFogDepth(Vector3 position)
    {
        if (activeRepellers.Count == 0)
            RebuildRepellersFromScene();

        float minDistanceToBoundary = float.PositiveInfinity;
        bool hasAnyRepeller = false;

        for (int i = activeRepellers.Count - 1; i >= 0; i--)
        {
            FogRepeller repeller = activeRepellers[i];
            if (repeller == null)
            {
                activeRepellers.RemoveAt(i);
                continue;
            }

            if (!repeller.isActiveAndEnabled)
                continue;
            if (!repeller.createsSafeZone)
                continue;

            hasAnyRepeller = true;
            float distanceToBoundary = Vector3.Distance(position, repeller.GetDrawPosition()) - repeller.clearRadius;
            if (distanceToBoundary < minDistanceToBoundary)
                minDistanceToBoundary = distanceToBoundary;
        }

        if (!hasAnyRepeller)
            return float.PositiveInfinity;

        return Mathf.Max(0f, minDistanceToBoundary);
    }

    public bool IsPositionInLight(Vector3 position)
    {
        return GetFogDepth(position) == 0f;
    }

    public bool IsPositionInFog(Vector3 position)
    {
        return GetFogDepth(position) > 0f;
    }

    public bool IsPositionInEnemyFog(Vector3 position)
    {
        return GetFogDepth(position) > GetEnemySoftEdgePadding();
    }

    public bool IsPositionInLanternLight(Vector3 position)
    {
        if (activeRepellers.Count == 0)
            RebuildRepellersFromScene();

        for (int i = activeRepellers.Count - 1; i >= 0; i--)
        {
            FogRepeller repeller = activeRepellers[i];
            if (repeller == null)
            {
                activeRepellers.RemoveAt(i);
                continue;
            }

            if (!repeller.isActiveAndEnabled)
                continue;
            if (repeller.createsSafeZone)
                continue;

            float radius = Mathf.Max(0f, repeller.clearRadius);
            Vector3 delta = position - repeller.GetDrawPosition();
            if (delta.sqrMagnitude <= radius * radius)
                return true;
        }

        return false;
    }

    private float GetEnemySoftEdgePadding()
    {
        if (fogMaskDrawer == null)
            fogMaskDrawer = FindFirstObjectByType<FogMaskDrawer>();

        if (fogMaskDrawer == null)
            return Mathf.Max(0f, enemySoftEdgeFallbackPadding);

        Transform fogSprite = fogMaskDrawer.fogSprite;
        float minScale = 100f;
        if (fogSprite != null)
            minScale = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(fogSprite.localScale.x), Mathf.Abs(fogSprite.localScale.y)));

        float transitionPixels = Mathf.Lerp(1f, 12f, Mathf.Clamp01(fogMaskDrawer.edgeSoftness));
        float transitionWorld = transitionPixels / Mathf.Max(1f, fogMaskDrawer.textureSize) * minScale;
        return Mathf.Max(0f, transitionWorld * 0.5f, enemySoftEdgeFallbackPadding);
    }
}
