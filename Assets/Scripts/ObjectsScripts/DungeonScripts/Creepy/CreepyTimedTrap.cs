using System.Collections;
using DG.Tweening;
using UnityEngine;

public enum CreepyTimedTrapType
{
    Mouth,
    Eye,
    Tentacle
}

[DisallowMultipleComponent]
public class CreepyTimedTrap : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CreepyTimedTrapType trapType;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CreepyWarningDamageZone warningZone;
    [SerializeField] private CreepyWarningDamageZone damageZone;

    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite warningSprite;
    [SerializeField] private Sprite activeSprite;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float idleTime = 1f;
    [Min(0f)] [SerializeField] private float warningTime = 0.5f;
    [Min(0.02f)] [SerializeField] private float activeTime = 0.3f;
    [Min(0f)] [SerializeField] private float cooldownTime = 0.1f;

    [Header("Damage")]
    [Min(0)] [SerializeField] private int damage = 10;

    [Header("Feedback")]
    [SerializeField] private bool shakeOnWarning;
    [Min(0f)] [SerializeField] private float shakeAmount = 0.035f;
    [Min(0.1f)] [SerializeField] private float shakeSpeed = 35f;
    [SerializeField] private bool punchOnActive = true;
    [Min(1f)] [SerializeField] private float activePunchScale = 1.08f;
    [SerializeField] private Vector3 activeZonePunchScale = new Vector3(0.18f, 0.18f, 0f);

    [Header("Generated Zone")]
    [SerializeField] private bool generateRoundZone;
    [Min(0.05f)] [SerializeField] private float roundZoneRadius = 1.1f;
    [Min(1f)] [SerializeField] private float roundZoneRadiusMultiplier = 1.35f;
    [SerializeField] private Color roundWarningColor = new Color(0.78f, 0.28f, 1f, 0.34f);
    [SerializeField] private Color roundActiveColor = new Color(1f, 0.72f, 0.95f, 0.58f);
    [SerializeField] private int roundZoneSortingOrder = 11;

    private Coroutine trapRoutine;
    private Tween activePunchTween;
    private Vector3 visualStartLocalPosition;
    private Vector3 visualStartLocalScale = Vector3.one;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.transform;

        if (damageZone == null)
            damageZone = warningZone;

        if (visualRoot != null)
        {
            visualStartLocalPosition = visualRoot.localPosition;
            visualStartLocalScale = visualRoot.localScale;
        }

        ConfigureGeneratedZoneIfNeeded();
        ApplyDamageValue();
        SetIdleState();
    }

    private void OnEnable()
    {
        trapRoutine = StartCoroutine(TrapLoop());
    }

    private void OnDisable()
    {
        if (trapRoutine != null)
        {
            StopCoroutine(trapRoutine);
            trapRoutine = null;
        }

        activePunchTween?.Kill();
        SetIdleState();
    }

    private IEnumerator TrapLoop()
    {
        while (enabled)
        {
            SetIdleState();
            if (idleTime > 0f)
                yield return new WaitForSeconds(idleTime);

            yield return WarningPhase();

            yield return ActivePhase();

            SetIdleState();
            if (cooldownTime > 0f)
                yield return new WaitForSeconds(cooldownTime);
        }
    }

    private IEnumerator WarningPhase()
    {
        SetWarningState();

        float timer = 0f;
        while (timer < warningTime)
        {
            timer += Time.deltaTime;

            if (shakeOnWarning && visualRoot != null)
            {
                float time = Time.time * shakeSpeed;
                float x = (Mathf.PerlinNoise(time, 0f) - 0.5f) * shakeAmount;
                float y = (Mathf.PerlinNoise(0f, time) - 0.5f) * shakeAmount;
                visualRoot.localPosition = visualStartLocalPosition + new Vector3(x, y, 0f);
            }

            yield return null;
        }

        ResetVisualPosition();
    }

    private IEnumerator ActivePhase()
    {
        SetActiveState();

        float timer = 0f;
        while (timer < activeTime)
        {
            timer += Time.deltaTime;

            yield return null;
        }

        ResetVisualTransform();
    }

    private void SetIdleState()
    {
        SetSprite(closedSprite);
        SetZoneWarning(false);
        SetZoneDamage(false);
        ResetVisualTransform();
    }

    private void SetWarningState()
    {
        SetSprite(warningSprite != null ? warningSprite : closedSprite);
        SetZoneWarning(true);
        SetZoneDamage(false);
        warningZone?.PlayWarningAppear();
    }

    private void SetActiveState()
    {
        SetSprite(activeSprite != null ? activeSprite : closedSprite);
        SetZoneWarning(false);
        SetZoneDamage(true);
        damageZone?.PlayActivePunch();

        if (punchOnActive && visualRoot != null)
        {
            activePunchTween?.Kill();
            visualRoot.localScale = visualStartLocalScale;
            activePunchTween = visualRoot
                .DOPunchScale(Vector3.one * (activePunchScale - 1f), 0.16f, 6, 0.45f)
                .SetEase(Ease.OutQuad);
        }
    }

    private void ApplyDamageValue()
    {
        if (warningZone != null)
            warningZone.SetDamage(damage);

        if (damageZone != null && damageZone != warningZone)
            damageZone.SetDamage(damage);
    }

    private void SetZoneWarning(bool visible)
    {
        if (warningZone != null)
            warningZone.SetWarningVisible(visible);

        if (damageZone != null && damageZone != warningZone)
            damageZone.SetWarningVisible(visible);
    }

    private void SetZoneDamage(bool active)
    {
        if (damageZone != null)
            damageZone.SetDamageActive(active);
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void ResetVisualPosition()
    {
        if (visualRoot != null)
            visualRoot.localPosition = visualStartLocalPosition;
    }

    private void ResetVisualTransform()
    {
        ResetVisualPosition();

        if (visualRoot != null)
            visualRoot.localScale = visualStartLocalScale;
    }

    private void ConfigureGeneratedZoneIfNeeded()
    {
        if (!generateRoundZone || warningZone == null)
            return;

        warningZone.ConfigureGeneratedCircle(
            roundZoneRadius * roundZoneRadiusMultiplier,
            roundWarningColor,
            roundActiveColor,
            roundZoneSortingOrder
        );
    }
}
