using UnityEngine;

[DisallowMultipleComponent]
public class Door : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private Collider2D interactionCollider;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 4f;
    [SerializeField] private float openSlideDistance = 0.5f;

    private Quaternion closedLocalRotation;
    private Vector3 closedLocalPosition;
    private float closedAngle;
    private Vector3Int firstWallDirection = Vector3Int.left;
    private Vector3Int secondWallDirection = Vector3Int.right;
    private bool isOpen;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

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
        closedLocalPosition = visualRoot.localPosition;

        if (interactionCollider != null)
            interactionCollider.isTrigger = true;
    }

    public void Initialize(Vector3Int firstDirection, Vector3Int secondDirection)
    {
        if (firstDirection != Vector3Int.zero)
            firstWallDirection = firstDirection;

        if (secondDirection != Vector3Int.zero)
            secondWallDirection = secondDirection;

        closedAngle = IsHorizontalDoor() ? 90f : 0f;
        closedLocalRotation = Quaternion.Euler(0f, 0f, closedAngle);
        closedLocalPosition = Vector3.zero;
        if (!isOpen)
        {
            visualRoot.localRotation = closedLocalRotation;
            visualRoot.localPosition = closedLocalPosition;
        }
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

        Vector3Int direction = Random.value < 0.5f ? firstWallDirection : secondWallDirection;
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, GetOpenAngle(direction));
        visualRoot.localPosition = GetOpenLocalPosition();
        AudioController.Instance?.PlayInteract();
    }

    void Close()
    {
        isOpen = false;
        visualRoot.localRotation = closedLocalRotation;
        visualRoot.localPosition = closedLocalPosition;

        if (blockingCollider != null)
            blockingCollider.enabled = true;

        AudioController.Instance?.PlayInteract();
    }

    float GetOpenAngle(Vector3Int direction)
    {
        if (IsHorizontalDoor())
            return direction == Vector3Int.left ? closedAngle - 90f : closedAngle + 90f;

        return direction == Vector3Int.up ? closedAngle + 90f : closedAngle - 90f;
    }

    bool IsHorizontalDoor()
    {
        return (firstWallDirection == Vector3Int.left && secondWallDirection == Vector3Int.right) ||
               (firstWallDirection == Vector3Int.right && secondWallDirection == Vector3Int.left);
    }

    Vector3 GetOpenLocalPosition()
    {
        float offset = Mathf.Max(0f, openSlideDistance);
        return IsHorizontalDoor() ? new Vector3(-offset, 0f, 0f) : new Vector3(0f, offset, 0f);
    }
}
