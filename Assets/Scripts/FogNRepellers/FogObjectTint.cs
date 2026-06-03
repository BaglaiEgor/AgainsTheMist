using UnityEngine;

public class FogObjectTint : MonoBehaviour
{
    [SerializeField] private Color fogColor = new Color32(0x6F, 0x7F, 0x72, 0xFF);
    [SerializeField] private float updateInterval = 0.15f;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float nextUpdateTime;
    private bool isTinted;

    void Awake()
    {
        CacheRenderers();
    }

    void OnEnable()
    {
        CacheRenderers();
        nextUpdateTime = 0f;
        UpdateTint(true);
    }

    void Update()
    {
        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + Mathf.Max(0.02f, updateInterval);
        UpdateTint(false);
    }

    void OnDisable()
    {
        SetTint(false);
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }
    }

    void UpdateTint(bool force)
    {
        FogSystem fogSystem = FogSystem.Instance;
        bool shouldTint = fogSystem != null && fogSystem.IsPositionInFog(transform.position);

        if (!force && shouldTint == isTinted)
            return;

        SetTint(shouldTint);
    }

    void SetTint(bool tint)
    {
        if (renderers == null || originalColors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
                continue;

            Color current = spriteRenderer.color;
            Color target = tint ? fogColor : originalColors[i];
            target.a = current.a;
            spriteRenderer.color = target;
        }

        isTinted = tint;
    }

    public static void EnsureOn(GameObject target)
    {
        if (target == null)
            return;

        if (target.GetComponentInChildren<SpriteRenderer>(true) == null)
            return;

        if (target.GetComponent<FogObjectTint>() == null)
            target.AddComponent<FogObjectTint>();
    }
}
