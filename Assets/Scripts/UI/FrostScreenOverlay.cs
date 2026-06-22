using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FrostScreenOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerFogPressure playerFogPressure;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private Image centerOverlay;
    [SerializeField] private Image[] edgeOverlays;

    [Header("Intensity")]
    [Range(0f, 1f)] [SerializeField] private float baseZoneIntensity = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float maxCenterAlpha = 0.18f;
    [Range(0f, 1f)] [SerializeField] private float maxEdgeAlpha = 0.65f;
    [Min(0.01f)] [SerializeField] private float fadeSpeed = 4f;

    [Header("Debug")]
    [SerializeField] private bool logMissingReferences;

    private float currentIntensity;
    private bool warnedMissingOverlay;
    private bool warnedMissingPlayer;

    void Awake()
    {
        ResolveReferences();
        ApplyVisuals(0f);
    }

    void OnEnable()
    {
        ResolveReferences();
        ApplyVisuals(currentIntensity);
    }

    void Update()
    {
        ResolveReferences();

        float targetIntensity = GetTargetIntensity();
        currentIntensity = Mathf.MoveTowards(
            currentIntensity,
            targetIntensity,
            Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime
        );

        ApplyVisuals(currentIntensity);
    }

    float GetTargetIntensity()
    {
        if (EntryAndExit.IsPlayerInDungeon)
            return 0f;

        if (playerTransform == null)
        {
            WarnMissingPlayer();
            return 0f;
        }

        if (!FrostFogZone.IsAnyZoneActiveAtPosition(playerTransform.position))
            return 0f;

        float pressure = playerFogPressure != null ? playerFogPressure.NormalizedFogPressure : 0f;
        return Mathf.Clamp01(Mathf.Max(baseZoneIntensity, pressure));
    }

    void ApplyVisuals(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        if (overlayGroup == null)
        {
            WarnMissingOverlay();
            return;
        }

        overlayGroup.alpha = intensity;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        SetImageAlpha(centerOverlay, maxCenterAlpha);

        if (edgeOverlays == null)
            return;

        for (int i = 0; i < edgeOverlays.Length; i++)
            SetImageAlpha(edgeOverlays[i], maxEdgeAlpha);
    }

    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
        image.raycastTarget = false;
    }

    void ResolveReferences()
    {
        if (overlayGroup == null)
            overlayGroup = GetComponent<CanvasGroup>();

        if (playerFogPressure == null)
            playerFogPressure = FindFirstObjectByType<PlayerFogPressure>();

        if (playerTransform == null && playerFogPressure != null)
            playerTransform = playerFogPressure.transform;
    }

    void WarnMissingOverlay()
    {
        if (!logMissingReferences || warnedMissingOverlay)
            return;

        warnedMissingOverlay = true;
        Debug.LogWarning("FrostScreenOverlay: назначь overlayGroup в Inspector или добавь CanvasGroup на этот объект.", this);
    }

    void WarnMissingPlayer()
    {
        if (!logMissingReferences || warnedMissingPlayer)
            return;

        warnedMissingPlayer = true;
        Debug.LogWarning("FrostScreenOverlay: не найден playerTransform или PlayerFogPressure.", this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        baseZoneIntensity = Mathf.Clamp01(baseZoneIntensity);
        maxCenterAlpha = Mathf.Clamp01(maxCenterAlpha);
        maxEdgeAlpha = Mathf.Clamp01(maxEdgeAlpha);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
    }
#endif
}
