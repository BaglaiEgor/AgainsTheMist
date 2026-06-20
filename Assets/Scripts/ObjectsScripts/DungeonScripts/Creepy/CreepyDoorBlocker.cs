using UnityEngine;

[DisallowMultipleComponent]
public class CreepyDoorBlocker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Collider2D blockingCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject visualRoot;

    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Open State")]
    [SerializeField] private bool startOpen;
    [SerializeField] private bool hideVisualWhenOpen;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 0.5f, 0f);

    private bool isOpen;
    private Vector3 closedVisualLocalPosition;

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
    }

    private void Start()
    {
        SetOpen(startOpen, false);
    }

    public void Open()
    {
        SetOpen(true, true);
    }

    public void Close()
    {
        SetOpen(false, true);
    }

    public void SetOpen(bool open)
    {
        SetOpen(open, true);
    }

    private void SetOpen(bool open, bool invokeEvents)
    {
        if (isOpen == open && invokeEvents)
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

        if (visualRoot != null)
        {
            visualRoot.SetActive(!isOpen || !hideVisualWhenOpen);
            visualRoot.transform.localPosition = isOpen ? closedVisualLocalPosition + openOffset : closedVisualLocalPosition;
        }
    }
}
