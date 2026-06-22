using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepyWarningDamageZone : MonoBehaviour
{
    private const int GeneratedCircleSize = 128;

    [Header("Refs")]
    [SerializeField] private SpriteRenderer warningRenderer;
    [SerializeField] private SpriteRenderer activeRenderer;

    [Header("Timing")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [Min(0f)] [SerializeField] private float idleTime = 1f;
    [Min(0f)] [SerializeField] private float warningDelay = 0.7f;
    [Min(0.02f)] [SerializeField] private float activeTime = 0.2f;

    [Header("Damage")]
    [Min(0)] [SerializeField] private int damage = 10;
    [SerializeField] private bool damageOnlyOncePerPulse = true;
    [Min(0.02f)] [SerializeField] private float damageCooldown = 0.4f;

    [Header("Visual Pulse")]
    [SerializeField] private bool pulseWarningVisual = true;
    [SerializeField] private bool pulseActiveVisual = true;
    [Min(0.5f)] [SerializeField] private float warningPulseSpeed = 7f;
    [Min(0.5f)] [SerializeField] private float activePulseSpeed = 14f;
    [Range(0f, 1f)] [SerializeField] private float warningPulseStrength = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float activePulseStrength = 0.22f;

    [Header("Creepy Palette")]
    [SerializeField] private bool useCreepyPalette = true;
    [SerializeField] private Color creepyWarningColor = new Color(0.72f, 0.18f, 0.95f, 0.24f);
    [SerializeField] private Color creepyActiveColor = new Color(1f, 0.35f, 0.95f, 0.42f);
    [Header("Tween Feedback")]
    [Min(0.01f)] [SerializeField] private float warningAppearDuration = 0.18f;
    [SerializeField] private Vector3 activePunchScale = new Vector3(0.18f, 0.18f, 0f);

    private readonly List<PlayerHealth> playersInside = new();
    private readonly HashSet<PlayerHealth> damagedThisPulse = new();
    private readonly Dictionary<PlayerHealth, float> nextDamageTimeByPlayer = new();
    private Collider2D zoneCollider;
    private float generatedRadius = 0.75f;
    private Coroutine routine;
    private bool damageActive;
    private bool warningVisible;
    private Color warningBaseColor = Color.white;
    private Color activeBaseColor = Color.white;
    private Vector3 warningBaseScale = Vector3.one;
    private Vector3 activeBaseScale = Vector3.one;
    private Tween warningTween;
    private Tween activeTween;
    private static Sprite generatedCircleSprite;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;

        if (warningRenderer == null)
            warningRenderer = GetComponentInChildren<SpriteRenderer>();

        CacheVisualDefaults();
        SetWarningVisible(false);
        SetDamageActive(false);
    }

    private void Update()
    {
        if (warningVisible && pulseWarningVisual)
            AnimateRendererPulse(warningRenderer, warningBaseColor, warningBaseScale, warningPulseSpeed, warningPulseStrength);

        if (damageActive && pulseActiveVisual)
            AnimateRendererPulse(activeRenderer, activeBaseColor, activeBaseScale, activePulseSpeed, activePulseStrength);

        if (damageActive)
            ApplyDamage();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            StartZone();
    }

    private void OnDisable()
    {
        StopZone();
        playersInside.Clear();
        damagedThisPulse.Clear();
        nextDamageTimeByPlayer.Clear();
        SetDamageActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && !playersInside.Contains(playerHealth))
            playersInside.Add(playerHealth);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        playersInside.Remove(playerHealth);
        damagedThisPulse.Remove(playerHealth);
        nextDamageTimeByPlayer.Remove(playerHealth);
    }

    public void StartZone()
    {
        if (routine != null)
            return;

        routine = StartCoroutine(ZoneLoop());
    }

    public void StopZone()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        SetWarningVisible(false);
        SetDamageActive(false);
    }

    public void PlayOnce()
    {
        if (routine != null)
            StopZone();

        routine = StartCoroutine(ZonePulse(false));
    }

    public void SetDamage(int value)
    {
        damage = Mathf.Max(0, value);
    }

    public void SetWarningVisible(bool visible)
    {
        warningVisible = visible;

        if (warningRenderer != null)
        {
            warningRenderer.enabled = visible;
            if (visible)
            {
                warningRenderer.color = warningBaseColor;
                warningRenderer.transform.localScale = warningBaseScale;
            }
        }
    }

    public void SetDamageActive(bool active)
    {
        damageActive = active;

        if (active)
            damagedThisPulse.Clear();

        if (activeRenderer != null)
        {
            activeRenderer.enabled = active;
            if (active)
            {
                activeRenderer.color = activeBaseColor;
                activeRenderer.transform.localScale = activeBaseScale;
            }
        }

        if (active)
            ApplyDamage();
    }

    public void PlayWarningAppear()
    {
        if (warningRenderer == null)
            return;

        warningTween?.Kill();
        warningRenderer.transform.localScale = warningBaseScale * 0.82f;
        warningTween = warningRenderer.transform
            .DOScale(warningBaseScale, Mathf.Max(0.01f, warningAppearDuration))
            .SetEase(Ease.OutBack);
    }

    public void PlayActivePunch()
    {
        if (activeRenderer == null)
            return;

        activeTween?.Kill();
        activeRenderer.transform.localScale = activeBaseScale;
        activeTween = activeRenderer.transform
            .DOPunchScale(activePunchScale, 0.16f, 6, 0.45f)
            .SetEase(Ease.OutQuad);
    }

    public void ConfigureGeneratedCircle(float radius, Color warningColor, Color activeColor, int sortingOrder)
    {
        radius = Mathf.Max(0.05f, radius);
        HideExistingRenderers();

        warningRenderer = CreateCircleRenderer("Generated Warning Circle", warningColor, sortingOrder);
        activeRenderer = CreateCircleRenderer("Generated Damage Circle", activeColor, sortingOrder + 1);
        warningRenderer.transform.localScale = Vector3.one * radius * 2f;
        activeRenderer.transform.localScale = Vector3.one * radius * 2f;

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
            boxCollider.enabled = false;

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider == null)
            circleCollider = gameObject.AddComponent<CircleCollider2D>();

        circleCollider.isTrigger = true;
        circleCollider.radius = radius;
        circleCollider.enabled = true;
        zoneCollider = circleCollider;
        generatedRadius = radius;

        CacheVisualDefaults();
        SetWarningVisible(false);
        SetDamageActive(false);
    }

    private IEnumerator ZoneLoop()
    {
        do
        {
            if (idleTime > 0f)
                yield return new WaitForSeconds(idleTime);

            yield return ZonePulse(true);
        }
        while (loop);

        routine = null;
    }

    private IEnumerator ZonePulse(bool keepRoutine)
    {
        damagedThisPulse.Clear();
        SetWarningVisible(true);

        if (warningDelay > 0f)
            yield return new WaitForSeconds(warningDelay);

        SetDamageActive(true);
        ApplyDamage();

        float timer = 0f;
        while (timer < activeTime)
        {
            timer += Time.deltaTime;
            ApplyDamage();
            yield return null;
        }

        SetDamageActive(false);
        SetWarningVisible(false);

        if (!keepRoutine)
            routine = null;
    }

    private void ApplyDamage()
    {
        if (!damageActive || damage <= 0)
            return;

        RefreshPlayersInsideFromOverlap();

        for (int i = playersInside.Count - 1; i >= 0; i--)
        {
            PlayerHealth playerHealth = playersInside[i];
            if (playerHealth == null)
            {
                playersInside.RemoveAt(i);
                continue;
            }

            if (!IsPlayerStillInside(playerHealth))
            {
                playersInside.RemoveAt(i);
                damagedThisPulse.Remove(playerHealth);
                nextDamageTimeByPlayer.Remove(playerHealth);
                continue;
            }

            if (damageOnlyOncePerPulse && damagedThisPulse.Contains(playerHealth))
                continue;

            if (!damageOnlyOncePerPulse &&
                nextDamageTimeByPlayer.TryGetValue(playerHealth, out float nextDamageTime) &&
                Time.time < nextDamageTime)
                continue;

            playerHealth.TakeDamage(damage);
            damagedThisPulse.Add(playerHealth);
            nextDamageTimeByPlayer[playerHealth] = Time.time + damageCooldown;
        }
    }

    private void RefreshPlayersInsideFromOverlap()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider == null || !zoneCollider.enabled)
            return;

        float radius = ResolveOverlapRadius();
        Collider2D[] hits = Physics2D.OverlapCircleAll(zoneCollider.bounds.center, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && !playersInside.Contains(playerHealth))
                playersInside.Add(playerHealth);
        }
    }

    private bool IsPlayerStillInside(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return false;

        if (zoneCollider == null || !zoneCollider.enabled)
            return true;

        Collider2D playerCollider = playerHealth.GetComponent<Collider2D>();
        if (playerCollider == null)
            playerCollider = playerHealth.GetComponentInChildren<Collider2D>();
        if (playerCollider == null)
            return zoneCollider.OverlapPoint(playerHealth.transform.position);

        Bounds zoneBounds = zoneCollider.bounds;
        zoneBounds.Expand(0.05f);
        return zoneBounds.Intersects(playerCollider.bounds);
    }

    private float ResolveOverlapRadius()
    {
        CircleCollider2D circleCollider = zoneCollider as CircleCollider2D;
        if (circleCollider != null)
            return Mathf.Max(0.05f, circleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));

        return Mathf.Max(0.05f, generatedRadius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
    }

    private void CacheVisualDefaults()
    {
        if (warningRenderer != null)
        {
            warningBaseColor = useCreepyPalette ? creepyWarningColor : warningRenderer.color;
            warningBaseScale = warningRenderer.transform.localScale;
            warningRenderer.color = warningBaseColor;
        }

        if (activeRenderer != null)
        {
            activeBaseColor = useCreepyPalette ? creepyActiveColor : activeRenderer.color;
            activeBaseScale = activeRenderer.transform.localScale;
            activeRenderer.color = activeBaseColor;
        }
    }

    private void OnDestroy()
    {
        warningTween?.Kill();
        activeTween?.Kill();
    }

    private void AnimateRendererPulse(SpriteRenderer renderer, Color baseColor, Vector3 baseScale, float speed, float strength)
    {
        if (renderer == null || !renderer.enabled)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
        Color color = baseColor;
        color.a = baseColor.a * Mathf.Lerp(1f - strength, 1f, wave);
        renderer.color = color;

        float scaleWave = 1f + strength * 0.18f * Mathf.Sin(Time.time * speed * 1.2f);
        renderer.transform.localScale = baseScale * scaleWave;
    }

    private void HideExistingRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = false;
    }

    private SpriteRenderer CreateCircleRenderer(string objectName, Color color, int sortingOrder)
    {
        Transform existing = transform.Find(objectName);
        GameObject circleObject = existing != null ? existing.gameObject : new GameObject(objectName);
        circleObject.transform.SetParent(transform, false);
        circleObject.transform.localPosition = Vector3.zero;
        circleObject.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer = circleObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = circleObject.AddComponent<SpriteRenderer>();

        renderer.sprite = GetGeneratedCircleSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    private static Sprite GetGeneratedCircleSprite()
    {
        if (generatedCircleSprite != null)
            return generatedCircleSprite;

        Texture2D texture = new Texture2D(GeneratedCircleSize, GeneratedCircleSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (GeneratedCircleSize - 1) * 0.5f;
        float radius = center - 3f;
        float ringRadius = radius * 0.78f;
        float ringWidth = 4.5f;
        float outerGlowWidth = 11f;
        float centerGlowRadius = radius * 0.28f;

        for (int y = 0; y < GeneratedCircleSize; y++)
        {
            for (int x = 0; x < GeneratedCircleSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / ringWidth);
                ring = Mathf.SmoothStep(0f, 1f, ring) * 0.78f;

                float outerGlow = 1f - Mathf.Clamp01(Mathf.Abs(distance - ringRadius) / outerGlowWidth);
                outerGlow = Mathf.SmoothStep(0f, 1f, outerGlow) * 0.12f;

                float centerGlow = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, centerGlowRadius));
                centerGlow = Mathf.SmoothStep(0f, 1f, centerGlow) * 0.16f;

                float edgeFade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(radius - 4f, radius, distance));
                float alpha = distance <= radius ? Mathf.Max(ring, outerGlow, centerGlow) * edgeFade : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        generatedCircleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, GeneratedCircleSize, GeneratedCircleSize),
            new Vector2(0.5f, 0.5f),
            GeneratedCircleSize
        );
        generatedCircleSprite.name = "Generated Creepy Circle Zone";
        return generatedCircleSprite;
    }
}
