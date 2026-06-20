using System.Collections.Generic;
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

        EnsureCounterText();
    }

    private void OnEnable()
    {
        SubscribeToReceivers();
        RefreshDoor();
    }

    private void OnDisable()
    {
        UnsubscribeFromReceivers();
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
            visualRoot.SetActive(!isOpen);

        if (visualRoot != null)
            visualRoot.transform.localPosition = isOpen ? closedVisualLocalPosition + openOffset : closedVisualLocalPosition;

        AudioController.Instance?.PlayDungeonDoor(transform.position);
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
}
