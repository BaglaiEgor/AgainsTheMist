using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonFireTrap : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Light2D fireLight;

    [Header("Sprites")]
    [SerializeField] private Sprite prepareFrame1;
    [SerializeField] private Sprite prepareFrame2;
    [SerializeField] private Sprite prepareFrame3;
    [SerializeField] private Sprite activeFrame4;

    [Header("Timing")]
    [Min(0.02f)] [SerializeField] private float frameTime = 0.15f;
    [Min(0f)] [SerializeField] private float idleTime = 1f;
    [Min(0.02f)] [SerializeField] private float activeTime = 0.6f;

    [Header("Damage")]
    [Min(0)] [SerializeField] private int damage = 15;
    [Min(0.02f)] [SerializeField] private float damageCooldown = 0.4f;

    [Header("Light")]
    [Min(0f)] [SerializeField] private float activeLightIntensity = 1.4f;
    [Min(0f)] [SerializeField] private float activeLightOuterRadius = 3f;
    [Range(0f, 1f)] [SerializeField] private float lightFlickerAmount = 0.25f;
    [Min(0.1f)] [SerializeField] private float lightFlickerSpeed = 18f;

    [Header("Visual Shake")]
    [SerializeField] private bool shakeVisualOnActive = true;
    [Min(0f)] [SerializeField] private float shakeAmount = 0.025f;
    [Min(0.1f)] [SerializeField] private float shakeSpeed = 35f;
    [SerializeField] private Vector3 activePunchScale = new Vector3(0.12f, 0.12f, 0f);
    [SerializeField] private Vector3 activeShakeStrength = new Vector3(0.04f, 0.04f, 0f);

    private readonly List<PlayerHealth> playersInside = new();
    private readonly Dictionary<PlayerHealth, float> nextDamageTimeByPlayer = new();
    private bool isActive;
    private float lightNoiseOffset;
    private float shakeNoiseOffset;
    private Vector3 visualStartLocalPosition;
    private Vector3 visualStartLocalScale = Vector3.one;
    private Tween activePunchTween;
    private Tween activeShakeTween;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.transform;

        if (fireLight == null)
            fireLight = GetComponentInChildren<Light2D>();

        Collider2D damageCollider = GetComponent<Collider2D>();
        damageCollider.isTrigger = true;

        lightNoiseOffset = Random.Range(0f, 100f);
        shakeNoiseOffset = Random.Range(0f, 100f);
        if (visualRoot != null)
        {
            visualStartLocalPosition = visualRoot.localPosition;
            visualStartLocalScale = visualRoot.localScale;
        }

        SetSprite(prepareFrame1);
        SetLightActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(TrapLoop());
    }

    private void OnDisable()
    {
        isActive = false;
        SetLightActive(false);
        KillActiveTweens();
        ResetVisualPosition();
    }

    private void Update()
    {
        if (!isActive)
            return;

        FlickerLight();
        ShakeVisual();
        ApplyDamageToPlayersInside();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playersInside.Contains(playerHealth))
            return;

        playersInside.Add(playerHealth);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        playersInside.Remove(playerHealth);
        nextDamageTimeByPlayer.Remove(playerHealth);
    }

    private IEnumerator TrapLoop()
    {
        while (enabled)
        {
            isActive = false;
            SetLightActive(false);
            ResetVisualPosition();
            SetSprite(prepareFrame1);
            yield return new WaitForSeconds(idleTime);

            SetSprite(prepareFrame2);
            yield return new WaitForSeconds(frameTime);

            SetSprite(prepareFrame3);
            yield return new WaitForSeconds(frameTime);

            SetSprite(activeFrame4);
            SetLightActive(true);
            PlayActiveFeedback();
            isActive = true;
            AudioController.Instance?.PlayDungeonFire(transform.position);
            ApplyDamageToPlayersInside();
            yield return new WaitForSeconds(activeTime);
        }
    }

    private void ApplyDamageToPlayersInside()
    {
        if (damage <= 0)
            return;

        for (int i = playersInside.Count - 1; i >= 0; i--)
        {
            PlayerHealth playerHealth = playersInside[i];
            if (playerHealth == null)
            {
                playersInside.RemoveAt(i);
                continue;
            }

            if (nextDamageTimeByPlayer.TryGetValue(playerHealth, out float nextDamageTime) && Time.time < nextDamageTime)
                continue;

            playerHealth.TakeDamage(damage);
            nextDamageTimeByPlayer[playerHealth] = Time.time + damageCooldown;
        }
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void SetLightActive(bool active)
    {
        if (fireLight == null)
            return;

        fireLight.enabled = active;
        if (!active)
            return;

        fireLight.intensity = activeLightIntensity;
        fireLight.pointLightOuterRadius = activeLightOuterRadius;
    }

    private void FlickerLight()
    {
        if (fireLight == null || lightFlickerAmount <= 0f)
            return;

        float noise = Mathf.PerlinNoise(lightNoiseOffset, Time.time * lightFlickerSpeed);
        float multiplier = 1f + Mathf.Lerp(-lightFlickerAmount, lightFlickerAmount, noise);
        fireLight.intensity = activeLightIntensity * multiplier;
    }

    private void ShakeVisual()
    {
        if (!shakeVisualOnActive || visualRoot == null || shakeAmount <= 0f)
            return;

        float time = Time.time * shakeSpeed;
        float x = (Mathf.PerlinNoise(shakeNoiseOffset, time) - 0.5f) * shakeAmount;
        float y = (Mathf.PerlinNoise(shakeNoiseOffset + 10f, time) - 0.5f) * shakeAmount;
        visualRoot.localPosition = visualStartLocalPosition + new Vector3(x, y, 0f);
    }

    private void ResetVisualPosition()
    {
        if (visualRoot != null)
            visualRoot.localPosition = visualStartLocalPosition;
    }

    private void PlayActiveFeedback()
    {
        if (visualRoot == null)
            return;

        KillActiveTweens();
        visualRoot.localScale = visualStartLocalScale;
        activePunchTween = visualRoot
            .DOPunchScale(activePunchScale, 0.16f, 6, 0.45f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => visualRoot.localScale = visualStartLocalScale);

        activeShakeTween = visualRoot
            .DOShakePosition(0.14f, activeShakeStrength, 10, 55f, false, true)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => visualRoot.localPosition = visualStartLocalPosition);
    }

    private void KillActiveTweens()
    {
        activePunchTween?.Kill();
        activeShakeTween?.Kill();
        activePunchTween = null;
        activeShakeTween = null;

        if (visualRoot != null)
            visualRoot.localScale = visualStartLocalScale;
    }
}
