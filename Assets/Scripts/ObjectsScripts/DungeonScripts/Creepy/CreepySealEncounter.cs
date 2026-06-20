using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepySealEncounter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CreepySealRoomController roomController;
    [SerializeField] private CreepyDestructibleTarget linkedTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction")]
    [Min(0.1f)] [SerializeField] private float interactDistance = 1.6f;

    [Header("Visual")]
    [SerializeField] private Sprite activatedSprite;
    [SerializeField] private Sprite completedSprite;
    [SerializeField] private GameObject activeHint;

    private bool activated;
    private bool completed;

    public bool Activated => activated;
    public bool Completed => completed;

    private void Awake()
    {
        Collider2D sealCollider = GetComponent<Collider2D>();
        sealCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (roomController == null)
            roomController = GetComponentInParent<CreepySealRoomController>();

        if (activeHint != null)
            activeHint.SetActive(false);
    }

    private void Update()
    {
        if (!activated || completed || linkedTarget == null || !linkedTarget.IsDestroyed)
            return;

        Complete();
    }

    private void OnMouseDown()
    {
        TryActivate();
    }

    public bool TryActivate()
    {
        if (activated || completed)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return false;

        if (Vector2.Distance(player.transform.position, transform.position) > interactDistance)
            return false;

        Activate();
        return true;
    }

    public void Activate()
    {
        if (activated || completed)
            return;

        activated = true;

        if (spriteRenderer != null && activatedSprite != null)
            spriteRenderer.sprite = activatedSprite;

        if (activeHint != null)
            activeHint.SetActive(true);

        if (linkedTarget != null)
            linkedTarget.ActivateTarget();

        if (linkedTarget == null)
            Complete();
    }

    private void Complete()
    {
        if (completed)
            return;

        completed = true;

        if (activeHint != null)
            activeHint.SetActive(false);

        if (spriteRenderer != null && completedSprite != null)
            spriteRenderer.sprite = completedSprite;

        if (roomController != null)
            roomController.NotifySealCompleted(this);
    }
}
