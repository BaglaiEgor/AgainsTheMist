using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepyClickableActivator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction")]
    [Min(0.1f)] [SerializeField] private float interactDistance = 1.6f;
    [SerializeField] private bool disableAfterUse = true;

    [Header("Visual")]
    [SerializeField] private Sprite activatedSprite;

    [Header("Events")]
    [SerializeField] private UnityEvent onActivated;

    private bool activated;

    public bool Activated => activated;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnMouseDown()
    {
        TryActivate();
    }

    public bool TryActivate()
    {
        if (activated && disableAfterUse)
            return false;

        Transform player = FindPlayer();
        if (player == null)
            return false;

        if (Vector2.Distance(player.position, transform.position) > interactDistance)
            return false;

        Activate();
        return true;
    }

    public void Activate()
    {
        if (activated && disableAfterUse)
            return;

        activated = true;

        if (spriteRenderer != null && activatedSprite != null)
            spriteRenderer.sprite = activatedSprite;

        onActivated?.Invoke();
    }

    public void ResetActivator()
    {
        activated = false;
    }

    private Transform FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null ? player.transform : null;
    }
}
