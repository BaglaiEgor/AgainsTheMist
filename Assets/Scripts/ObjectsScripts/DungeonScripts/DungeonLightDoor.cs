using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class DungeonLightDoor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private bool autoCreateCounterText = true;

    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Receivers")]
    [SerializeField] private List<DungeonLightReceiver> requiredReceivers = new();

    [Header("Open State")]
    [SerializeField] private bool hideVisualWhenOpen;
    [SerializeField] private bool autoDetectVerticalDoor = true;
    [SerializeField] private bool isVerticalDoor;
    [SerializeField] private Vector3 verticalClosedVisualOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 verticalOpenVisualOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 horizontalOpenVisualOffset = new Vector3(0f, -0.5f, 0f);
    [Min(0.01f)] [SerializeField] private float doorTweenDuration = 0.18f;
    [SerializeField] private Vector3 doorPunchScale = new Vector3(0.08f, 0.08f, 0f);
    [SerializeField] private Vector3 doorShakeStrength = new Vector3(0.04f, 0.04f, 0f);

    private bool isOpen;
    private Vector3 closedVisualLocalPosition;
    private Tween moveTween;
    private Tween punchTween;
    private Tween shakeTween;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (blockingCollider == null)
            blockingCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.gameObject;

        if (visualRoot != null)
            closedVisualLocalPosition = visualRoot.transform.localPosition;

        EnsureCounterText();
    }

    private void OnEnable()
    {
        SubscribeToReceivers();
        ApplyDoorVisualPositionInstant();
        RefreshDoor();
    }

    private void OnDisable()
    {
        UnsubscribeFromReceivers();
        ResetDoorTweens();
    }

    private void SubscribeToReceivers()
    {
        for (int i = 0; i < requiredReceivers.Count; i++)
        {
            if (requiredReceivers[i] != null)
                requiredReceivers[i].StateChanged += HandleReceiverStateChanged;
        }
    }

    private void UnsubscribeFromReceivers()
    {
        for (int i = 0; i < requiredReceivers.Count; i++)
        {
            if (requiredReceivers[i] != null)
                requiredReceivers[i].StateChanged -= HandleReceiverStateChanged;
        }
    }

    private void HandleReceiverStateChanged(DungeonLightReceiver receiver)
    {
        RefreshDoor();
    }

    private void RefreshDoor()
    {
        int total = 0;
        int lit = 0;

        for (int i = 0; i < requiredReceivers.Count; i++)
        {
            DungeonLightReceiver receiver = requiredReceivers[i];
            if (receiver == null)
                continue;

            total++;
            if (receiver.IsLit)
                lit++;
        }

        if (counterText != null)
            counterText.text = $"{lit}/{total}";

        SetOpen(total > 0 && lit >= total);
    }

    private void SetOpen(bool open)
    {
        if (isOpen == open)
            return;

        isOpen = open;

        if (blockingCollider != null)
            blockingCollider.enabled = !isOpen;

        if (spriteRenderer != null)
        {
            Sprite targetSprite = isOpen ? openSprite : closedSprite;
            if (targetSprite != null)
                spriteRenderer.sprite = targetSprite;
        }

        if (visualRoot != null && hideVisualWhenOpen)
            visualRoot.SetActive(true);

        PlayDoorFeedback();

        AudioController.Instance?.PlayDungeonDoor(transform.position);
    }

    private void PlayDoorFeedback()
    {
        if (visualRoot == null)
            return;

        Transform visualTransform = visualRoot.transform;
        Vector3 targetPosition = GetDoorVisualTargetPosition(isOpen);

        moveTween?.Kill();
        punchTween?.Kill();
        shakeTween?.Kill();

        moveTween = visualTransform
            .DOLocalMove(targetPosition, Mathf.Max(0.01f, doorTweenDuration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                moveTween = null;
                if (hideVisualWhenOpen && isOpen && visualRoot != null)
                    visualRoot.SetActive(false);
            });

        punchTween = visualTransform
            .DOPunchScale(doorPunchScale, 0.16f, 6, 0.45f)
            .SetEase(Ease.OutQuad);

        shakeTween = visualTransform
            .DOShakePosition(0.12f, doorShakeStrength, 8, 45f, false, true)
            .SetEase(Ease.OutQuad);
    }

    private void EnsureCounterText()
    {
        if (counterText != null || !autoCreateCounterText)
            return;

        GameObject textObject = new GameObject("LightCounterText", typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 1f, 0f);

        counterText = textObject.GetComponent<TextMeshPro>();
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.fontSize = 3f;
        counterText.color = Color.white;
        counterText.text = "0/0"; 
    }

    private void ResetDoorTweens()
    {
        moveTween?.Kill();
        punchTween?.Kill();
        shakeTween?.Kill();

        if (visualRoot != null)
            visualRoot.transform.localPosition = GetDoorVisualTargetPosition(isOpen);
    }

    private void ApplyDoorVisualPositionInstant()
    {
        if (visualRoot != null)
            visualRoot.transform.localPosition = GetDoorVisualTargetPosition(isOpen);
    }

    private Vector3 GetDoorVisualTargetPosition(bool open)
    {
        if (IsVerticalDoor())
            return closedVisualLocalPosition + (open ? verticalOpenVisualOffset : verticalClosedVisualOffset);

        return open ? closedVisualLocalPosition + horizontalOpenVisualOffset : closedVisualLocalPosition;
    }

    private bool IsVerticalDoor()
    {
        if (!autoDetectVerticalDoor)
            return isVerticalDoor;

        float z = transform.eulerAngles.z;
        return Mathf.Abs(Mathf.DeltaAngle(z, 90f)) < 10f || Mathf.Abs(Mathf.DeltaAngle(z, 270f)) < 10f;
    }
}
