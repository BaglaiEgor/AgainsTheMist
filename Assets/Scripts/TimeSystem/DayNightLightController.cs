using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DayNightLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameTimeSystem timeSystem;
    [SerializeField] private Light2D globalLight;

    [Header("Light By Time")]
    [SerializeField] private bool usePreciseTime = true;
    [SerializeField] private float intensityMultiplier = 1f;
    [Header("Night Tuning")]
    [Range(0f, 1f)] [SerializeField] private float nightIntensityMultiplier = 0.4f;
    [SerializeField] private AnimationCurve intensityByTime = new AnimationCurve(
        new Keyframe(0.00f, 0.25f),
        new Keyframe(0.20f, 0.55f),
        new Keyframe(0.35f, 1.00f),
        new Keyframe(0.65f, 1.00f),
        new Keyframe(0.80f, 0.55f),
        new Keyframe(1.00f, 0.25f)
    );
    [SerializeField] private Gradient colorByTime;

    private bool forceIntensity;
    private float forcedIntensity;

    private void Reset()
    {
        globalLight = GetComponent<Light2D>();
        EnsureDefaultGradient();
    }

    private void Awake()
    {
        if (globalLight == null)
            globalLight = GetComponent<Light2D>();

        EnsureDefaultGradient();
    }

    private void Start()
    {
        ApplyLightNow();
    }

    private void Update()
    {
        ApplyLightNow();
    }

    public void ApplyLightNow()
    {
        if (timeSystem == null || globalLight == null)
            return;

        if (forceIntensity)
        {
            globalLight.intensity = forcedIntensity;
            return;
        }

        float t = usePreciseTime ? timeSystem.TimeOfDayPrecise01 : timeSystem.TimeOfDay01;
        t = Mathf.Clamp01(t);

        float intensity = intensityByTime != null ? intensityByTime.Evaluate(t) : 1f;
        float nightWeight = 1f - Mathf.Clamp01(intensity);
        float darkening = Mathf.Lerp(1f, Mathf.Clamp01(nightIntensityMultiplier), nightWeight);
        intensity *= darkening;
        globalLight.intensity = Mathf.Max(0f, intensity * Mathf.Max(0f, intensityMultiplier));

        if (colorByTime != null)
            globalLight.color = colorByTime.Evaluate(t);
    }

    public void ForceIntensity(float intensity)
    {
        forcedIntensity = Mathf.Max(0f, intensity);
        forceIntensity = true;

        if (globalLight != null)
            globalLight.intensity = forcedIntensity;
    }

    public void ClearForcedIntensity()
    {
        forceIntensity = false;
        ApplyLightNow();
    }

    private void EnsureDefaultGradient()
    {
        if (colorByTime == null)
            colorByTime = new Gradient();

        GradientColorKey[] colorKeys = colorByTime.colorKeys;
        GradientAlphaKey[] alphaKeys = colorByTime.alphaKeys;
        if (colorKeys != null && colorKeys.Length > 0 && alphaKeys != null && alphaKeys.Length > 0)
            return;

        colorByTime.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.25f, 0.32f, 0.55f), 0.00f),
                new GradientColorKey(new Color(0.95f, 0.75f, 0.55f), 0.22f),
                new GradientColorKey(new Color(1.00f, 0.98f, 0.90f), 0.50f),
                new GradientColorKey(new Color(0.95f, 0.75f, 0.55f), 0.78f),
                new GradientColorKey(new Color(0.25f, 0.32f, 0.55f), 1.00f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
    }

    private void OnValidate()
    {
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        nightIntensityMultiplier = Mathf.Clamp01(nightIntensityMultiplier);
        EnsureDefaultGradient();
    }
}
