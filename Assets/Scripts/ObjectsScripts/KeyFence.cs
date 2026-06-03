using UnityEngine;

[DisallowMultipleComponent]
public class KeyFence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private Collider2D interactionCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Interaction")]
    [SerializeField] private ItemData requiredKey;
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;

    private bool isOpen;

    public bool IsOpen => isOpen;
    public string InteractLabel => "Открыть";

    private void Awake()
    {
        if (blockingCollider == null)
            blockingCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (interactionCollider == null)
            interactionCollider = FindInteractionCollider();

        if (interactionCollider != null && interactionCollider != blockingCollider)
            interactionCollider.isTrigger = true;

        if (requiredKey == null)
            requiredKey = Resources.Load<ItemData>("Materials/Master key");

        RefreshVisual();
    }

    public bool CanInteract(Transform interactor)
    {
        if (isOpen)
            return false;

        if (interactor == null)
            return true;

        return Vector2.Distance(interactor.position, transform.position) <= interactDistance;
    }

    public bool CanShowTooltip(Transform interactor)
    {
        return CanInteract(interactor) && HasRequiredKey(FindInventory(interactor));
    }

    public bool TryOpen(Transform interactor, Inventory inventory)
    {
        if (!CanInteract(interactor) || !HasRequiredKey(inventory))
            return false;

        isOpen = true;
        RefreshVisual();
        AudioController.Instance?.PlayInteract();
        return true;
    }

    private bool HasRequiredKey(Inventory inventory)
    {
        return requiredKey != null && inventory != null && inventory.HasItem(requiredKey, 1);
    }

    private void RefreshVisual()
    {
        if (blockingCollider != null)
            blockingCollider.enabled = !isOpen;

        if (spriteRenderer != null)
        {
            Sprite targetSprite = isOpen ? openSprite : closedSprite;
            if (targetSprite != null)
                spriteRenderer.sprite = targetSprite;
        }
    }

    private Collider2D FindInteractionCollider()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i] != blockingCollider)
                return colliders[i];
        }

        return blockingCollider;
    }

    private static Inventory FindInventory(Transform source)
    {
        if (source == null)
            return null;

        Inventory inventory = source.GetComponent<Inventory>();
        if (inventory == null)
            inventory = source.GetComponentInParent<Inventory>();
        if (inventory == null)
            inventory = source.GetComponentInChildren<Inventory>();

        return inventory;
    }
}
