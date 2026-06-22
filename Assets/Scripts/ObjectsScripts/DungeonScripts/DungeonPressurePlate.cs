using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonPressurePlate : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite releasedSprite;
    [SerializeField] private Sprite pressedSprite;

    [Header("Glow")]
    [SerializeField] private bool enableGlow = true;
    [SerializeField] private Color releasedGlowColor = new Color(0.55f, 0.18f, 0.8f, 0.18f);
    [SerializeField] private Color pressedGlowColor = new Color(1f, 0.72f, 0.28f, 0.42f);
    [Min(1f)] [SerializeField] private float glowScale = 1.22f;
    [Min(0.1f)] [SerializeField] private float glowPulseSpeed = 8f;
    [Min(0f)] [SerializeField] private float pressedPunchTime = 0.16f;
    [Min(0.01f)] [SerializeField] private float plateTweenDuration = 0.08f;
    [SerializeField] private Vector3 pressedScale = new Vector3(0.92f, 0.92f, 1f);
    [SerializeField] private Vector3 pressPunchScale = new Vector3(0.08f, 0.08f, 0f);

    private readonly List<Component> pressers = new();
    private Collider2D plateCollider;
    private SpriteRenderer glowRenderer;
    private Transform visualTransform;
    private Tween plateTween;
    private Tween glowTween;
    private Vector3 baseVisualScale = Vector3.one;
    private Vector3 baseGlowScale = Vector3.one;
    private bool isPressed;
    private float pressedPunchTimer;

    public bool IsPressed => isPressed;
    public event Action<DungeonPressurePlate> StateChanged;

    private void Awake()
    {
        plateCollider = GetComponent<Collider2D>();
        plateCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        visualTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        baseVisualScale = visualTransform.localScale;
        EnsureGlowRenderer();
        RefreshVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Component presser = GetPresser(other);
        if (presser == null || pressers.Contains(presser))
            return;

        pressers.Add(presser);
        RefreshState();
        UpdateGlowPulse();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Component presser = GetPresser(other);
        if (presser == null)
            return;

        pressers.Remove(presser);
        RefreshState();
    }

    private void Update()
    {
        for (int i = pressers.Count - 1; i >= 0; i--)
        {
            if (pressers[i] == null)
                pressers.RemoveAt(i);
        }

        RefreshState();
    }

    private Component GetPresser(Collider2D other)
    {
        if (other == null)
            return null;

        DungeonPushBox pushBox = other.GetComponentInParent<DungeonPushBox>();
        if (pushBox != null)
            return pushBox;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
            return playerHealth;

        return null;
    }

    private void RefreshState()
    {
        bool nextPressed = pressers.Count > 0;
        if (isPressed == nextPressed)
            return;

        isPressed = nextPressed;
        if (isPressed)
            pressedPunchTimer = pressedPunchTime;

        RefreshVisual();
        PlayPressFeedback();

        if (isPressed)
            AudioController.Instance?.PlayDungeonPlate(transform.position);

        StateChanged?.Invoke(this);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        Sprite targetSprite = isPressed ? pressedSprite : releasedSprite;
        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;

        RefreshGlowVisual(targetSprite != null ? targetSprite : spriteRenderer.sprite);
    }

    private void EnsureGlowRenderer()
    {
        if (!enableGlow || spriteRenderer == null)
            return;

        Transform existing = transform.Find("Plate Glow");
        GameObject glowObject = existing != null ? existing.gameObject : new GameObject("Plate Glow");
        glowObject.transform.SetParent(spriteRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;

        glowRenderer = glowObject.GetComponent<SpriteRenderer>();
        if (glowRenderer == null)
            glowRenderer = glowObject.AddComponent<SpriteRenderer>();

        glowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        glowRenderer.enabled = true;
        baseGlowScale = glowRenderer.transform.localScale;
    }

    private void RefreshGlowVisual(Sprite sprite)
    {
        if (!enableGlow)
            return;

        EnsureGlowRenderer();
        if (glowRenderer == null)
            return;

        glowRenderer.sprite = sprite;
        glowRenderer.color = isPressed ? pressedGlowColor : releasedGlowColor;
        glowRenderer.transform.localScale = Vector3.one * glowScale;
        baseGlowScale = glowRenderer.transform.localScale;
    }

    private void UpdateGlowPulse()
    {
        if (!enableGlow || glowRenderer == null)
            return;

        if (pressedPunchTimer > 0f)
            pressedPunchTimer = Mathf.Max(0f, pressedPunchTimer - Time.deltaTime);

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * glowPulseSpeed);
        Color baseColor = isPressed ? pressedGlowColor : releasedGlowColor;
        baseColor.a *= Mathf.Lerp(0.72f, 1f, wave);
        glowRenderer.color = baseColor;

        float punch = pressedPunchTime > 0f ? pressedPunchTimer / pressedPunchTime : 0f;
        float scale = glowScale + Mathf.Lerp(0f, 0.12f, punch);
        glowRenderer.transform.localScale = Vector3.one * scale;
    }

    private void PlayPressFeedback()
    {
        if (visualTransform == null)
            return;

        plateTween?.Kill();
        if (isPressed)
        {
            visualTransform.localScale = baseVisualScale;
            plateTween = visualTransform
                .DOScale(Vector3.Scale(baseVisualScale, pressedScale), Mathf.Max(0.01f, plateTweenDuration))
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    plateTween = visualTransform
                        .DOPunchScale(pressPunchScale, Mathf.Max(0.01f, pressedPunchTime), 6, 0.45f)
                        .SetEase(Ease.OutQuad);
                });
        }
        else
        {
            plateTween = visualTransform
                .DOScale(baseVisualScale, Mathf.Max(0.01f, plateTweenDuration))
                .SetEase(Ease.OutQuad);
        }

        if (glowRenderer == null)
            return;

        glowTween?.Kill();
        glowTween = glowRenderer.transform
            .DOPunchScale(Vector3.one * (isPressed ? 0.18f : 0.08f), 0.14f, 6, 0.45f)
            .SetEase(Ease.OutQuad);
    }

    private void OnDisable()
    {
        plateTween?.Kill();
        glowTween?.Kill();

        if (visualTransform != null)
            visualTransform.localScale = baseVisualScale;

        if (glowRenderer != null)
            glowRenderer.transform.localScale = baseGlowScale;
    }
}
