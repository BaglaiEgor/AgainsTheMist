using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepySequencePuzzleButton : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CreepySequencePuzzle puzzle;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string playerTag = "Player";

    [Header("Button")]
    [SerializeField] private CreepySequenceSymbol symbol;
    [Min(0.1f)] [SerializeField] private float interactDistance = 1.6f;

    [Header("Visual")]
    [SerializeField] private Sprite releasedSprite;
    [SerializeField] private Sprite pressedSprite;

    private bool locked;

    private void Awake()
    {
        Collider2D buttonCollider = GetComponent<Collider2D>();
        buttonCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (puzzle == null)
            puzzle = GetComponentInParent<CreepySequencePuzzle>();

        RefreshVisual();
    }

    private void OnMouseDown()
    {
        TryPress();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() != null)
            TryPress();
    }

    public void SetLocked(bool value)
    {
        locked = value;
        RefreshVisual();
    }

    public bool TryPress()
    {
        if (locked || puzzle == null)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return false;

        if (Vector2.Distance(player.transform.position, transform.position) > interactDistance)
            return false;

        puzzle.PressButton(this, symbol);
        return true;
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        Sprite targetSprite = locked ? pressedSprite : releasedSprite;
        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }
}
