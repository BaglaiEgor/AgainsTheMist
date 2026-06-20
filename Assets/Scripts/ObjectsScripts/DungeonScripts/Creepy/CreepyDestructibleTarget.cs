using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepyDestructibleTarget : MonoBehaviour, IDamageable
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private GameObject visualRoot;

    [Header("Health")]
    [Min(1)] [SerializeField] private int maxHealth = 20;
    [SerializeField] private bool startInvulnerable;
    [Min(1)] [SerializeField] private int zeroDamageHitPower = 5;

    [Header("Visual")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite vulnerableSprite;
    [SerializeField] private Sprite destroyedSprite;
    [SerializeField] private bool hideWhenDestroyed = true;

    private int currentHealth;
    private bool vulnerable;
    private bool destroyed;

    public bool IsVulnerable => vulnerable;
    public bool IsDestroyed => destroyed;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        vulnerable = !startInvulnerable;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (targetCollider == null)
            targetCollider = GetComponent<Collider2D>();

        EnsureTriggerBody();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.gameObject;

        RefreshVisual();
    }

    public void TakeDamage(int damage)
    {
        if (destroyed || !vulnerable)
            return;

        int appliedDamage = damage > 0 ? damage : zeroDamageHitPower;
        currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
        HitFlashFeedback.PlayOn(gameObject, new Color(1f, 0.35f, 0.85f, 1f), 0.12f);

        if (currentHealth <= 0)
            DestroyTarget();
    }

    public void ActivateTarget()
    {
        if (destroyed)
            return;

        vulnerable = true;

        if (targetCollider != null)
            targetCollider.enabled = true;

        RefreshVisual();
        HitFlashFeedback.PlayOn(gameObject, new Color(0.55f, 1f, 0.7f, 1f), 0.12f);
    }

    public void DestroyTarget()
    {
        if (destroyed)
            return;

        destroyed = true;

        if (targetCollider != null)
            targetCollider.enabled = false;

        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (visualRoot != null)
            visualRoot.SetActive(!destroyed || !hideWhenDestroyed);

        if (spriteRenderer == null)
            return;

        if (destroyed && destroyedSprite != null)
            spriteRenderer.sprite = destroyedSprite;
        else if (vulnerable && vulnerableSprite != null)
            spriteRenderer.sprite = vulnerableSprite;
        else if (normalSprite != null)
            spriteRenderer.sprite = normalSprite;
    }

    private void EnsureTriggerBody()
    {
        if (targetCollider == null || !targetCollider.isTrigger)
            return;

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody2D>();

        if (targetRigidbody == null)
            targetRigidbody = gameObject.AddComponent<Rigidbody2D>();

        targetRigidbody.bodyType = RigidbodyType2D.Kinematic;
        targetRigidbody.simulated = true;
        targetRigidbody.gravityScale = 0f;
        targetRigidbody.freezeRotation = true;
    }
}
