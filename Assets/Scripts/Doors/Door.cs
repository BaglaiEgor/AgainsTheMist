using UnityEngine;

[DisallowMultipleComponent]
public class Door : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private Collider2D interactionCollider;
    [SerializeField] private Sprite openSprite;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 4f;

    [Header("Visual Offsets")]
    [SerializeField] private Vector3 horizontalClosedWorldOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 verticalClosedWorldOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 horizontalOpenWorldOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private Vector3 verticalOpenWorldOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private int openSortingOrder = 0;

    private Quaternion closedLocalRotation;
    private Sprite closedSprite;
    private int closedSortingOrder;
    private Vector3Int firstWallDirection = Vector3Int.left;
    private Vector3Int secondWallDirection = Vector3Int.right;
    private bool isOpen;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();

        if (blockingCollider == null)
            blockingCollider = GetComponent<Collider2D>();

        if (interactionCollider == null)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i] != blockingCollider)
                {
                    interactionCollider = colliders[i];
                    break;
                }
            }
        }

        closedLocalRotation = visualRoot.localRotation;
        if (visualRenderer != null)
        {
            closedSprite = visualRenderer.sprite;
            closedSortingOrder = visualRenderer.sortingOrder;
        }

        if (interactionCollider != null)
            interactionCollider.isTrigger = true;

        ApplyVisualState();
    }

    public void Initialize(Vector3Int firstDirection, Vector3Int secondDirection)
    {
        if (firstDirection != Vector3Int.zero)
            firstWallDirection = firstDirection;

        if (secondDirection != Vector3Int.zero)
            secondWallDirection = secondDirection;

        transform.rotation = Quaternion.Euler(0f, 0f, IsHorizontalDoor() ? 0f : 90f);
        closedLocalRotation = Quaternion.identity;
        ApplyVisualState();
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return true;

        return Vector2.Distance(interactor.position, transform.position) <= interactRadius;
    }

    public void Toggle()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    void Open()
    {
        isOpen = true;

        if (blockingCollider != null)
            blockingCollider.enabled = false;

        ApplyVisualState();
        AudioController.Instance?.PlayInteract(transform.position);
    }

    void Close()
    {
        isOpen = false;
        ApplyVisualState();

        if (blockingCollider != null)
            blockingCollider.enabled = true;

        AudioController.Instance?.PlayInteract(transform.position);
    }

    bool IsHorizontalDoor()
    {
        return (firstWallDirection == Vector3Int.left && secondWallDirection == Vector3Int.right) ||
               (firstWallDirection == Vector3Int.right && secondWallDirection == Vector3Int.left);
    }

    void ApplyVisualState()
    {
        if (visualRoot == null)
            return;

        visualRoot.localRotation = closedLocalRotation;
        visualRoot.localPosition = transform.InverseTransformVector(GetCurrentWorldOffset());
        if (visualRenderer != null)
            visualRenderer.sortingOrder = isOpen ? openSortingOrder : closedSortingOrder;

        SetSprite(isOpen ? openSprite : closedSprite);
    }

    Vector3 GetCurrentWorldOffset()
    {
        if (IsHorizontalDoor())
            return isOpen ? horizontalOpenWorldOffset : horizontalClosedWorldOffset;

        return isOpen ? verticalOpenWorldOffset : verticalClosedWorldOffset;
    }

    void SetSprite(Sprite sprite)
    {
        if (visualRenderer != null && sprite != null)
            visualRenderer.sprite = sprite;
    }
}
