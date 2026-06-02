using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonTeleportPlate : MonoBehaviour
{
    [System.Serializable]
    private class TeleportTarget
    {
        public Transform target = null;
        public Transform destinationPoint = null;
        public Vector3 fallbackPosition = Vector3.zero;
    }

    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite releasedSprite;
    [SerializeField] private Sprite pressedSprite;

    [Header("Teleport")]
    [SerializeField] private List<TeleportTarget> teleportTargets = new();
    [SerializeField] private bool triggerOnlyOnce;
    [SerializeField] private bool playerCanPress = true;
    [SerializeField] private bool pushBoxesCanPress = true;
    [SerializeField] private bool lightObjectsCanPress = true;

    private readonly List<Component> pressers = new();
    private Collider2D plateCollider;
    private bool isPressed;
    private bool hasTriggered;

    private void Awake()
    {
        plateCollider = GetComponent<Collider2D>();
        plateCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        RefreshVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Component presser = GetPresser(other);
        if (presser == null || pressers.Contains(presser))
            return;

        pressers.Add(presser);
        RefreshState();
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

        if (pushBoxesCanPress)
        {
            DungeonPushBox pushBox = other.GetComponentInParent<DungeonPushBox>();
            if (pushBox != null)
                return pushBox;
        }

        if (lightObjectsCanPress)
        {
            DungeonLightSource lightSource = other.GetComponentInParent<DungeonLightSource>();
            if (lightSource != null)
                return lightSource;

            DungeonLightMirror lightMirror = other.GetComponentInParent<DungeonLightMirror>();
            if (lightMirror != null)
                return lightMirror;
        }

        if (playerCanPress)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
                return playerHealth;
        }

        return null;
    }

    private void RefreshState()
    {
        bool nextPressed = pressers.Count > 0;
        if (isPressed == nextPressed)
            return;

        isPressed = nextPressed;
        RefreshVisual();

        if (isPressed)
        {
            AudioController.Instance?.PlayDungeonPlate();
            TryTeleportTargets();
        }
    }

    private void TryTeleportTargets()
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        hasTriggered = true;

        for (int i = 0; i < teleportTargets.Count; i++)
        {
            TeleportTarget teleportTarget = teleportTargets[i];
            if (teleportTarget == null || teleportTarget.target == null)
                continue;

            Vector3 targetPosition = teleportTarget.destinationPoint != null
                ? teleportTarget.destinationPoint.position
                : teleportTarget.fallbackPosition;

            Rigidbody2D rb = teleportTarget.target.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            teleportTarget.target.position = targetPosition;
        }
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        Sprite targetSprite = isPressed ? pressedSprite : releasedSprite;
        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }
}
