using System;
using System.Collections.Generic;
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

    private readonly List<Component> pressers = new();
    private Collider2D plateCollider;
    private bool isPressed;

    public bool IsPressed => isPressed;
    public event Action<DungeonPressurePlate> StateChanged;

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
        RefreshVisual();

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
    }
}
