using UnityEngine;

public enum FogEnemyBehaviorType
{
    Chaser = 0,
    HitAndRun = 1
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FogEnemy : MonoBehaviour, IDamageable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private FogEnemyBehaviorType behaviorType = FogEnemyBehaviorType.Chaser;

    [Header("Hit And Run")]
    [SerializeField] private float hitAndRunEngageDuration = 0.8f;
    [SerializeField] private float hitAndRunRetreatDuration = 0.9f;
    [SerializeField] private float hitAndRunRetreatDistance = 1.5f;
    [SerializeField] private float hitAndRunRetreatSpeedMultiplier = 1.35f;

    [Header("Combat")]
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int contactDamage = 5;
    [SerializeField] private float contactDamageInterval = 1f;

    [Header("Fog")]
    [SerializeField] private float despawnDelayInLight = 2f;
    [SerializeField] private float lanternProtectionBreakPressure = 70f;

    private Transform target;
    private int currentHealth;
    private float nextContactDamageTime;
    private float lightDespawnTimer;
    private bool hitAndRunRetreating;
    private float hitAndRunTimer;
    private bool isDead;

    public bool IsDead => isDead;

    void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);

        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.isTrigger = true;
    }

    void Update()
    {
        if (isDead)
            return;

        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        bool inFog = fogSystem.IsPositionInFog(transform.position);
        if (!inFog)
        {
            lightDespawnTimer += Time.deltaTime;
            if (lightDespawnTimer >= Mathf.Max(0f, despawnDelayInLight))
                Destroy(gameObject);
            return;
        }

        lightDespawnTimer = 0f;
        bool lanternRepelsEnemy = IsLanternRepelActive();
        if (lanternRepelsEnemy && fogSystem.IsPositionInLanternLight(transform.position))
        {
            ResetHitAndRunState();
            MoveAwayFromPlayer();
            return;
        }

        MoveByBehavior(fogSystem, lanternRepelsEnemy);
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth > 0)
            return;

        isDead = true;
        ResourceDrop resourceDrop = GetComponent<ResourceDrop>();
        if (resourceDrop != null)
            resourceDrop.DropNow();

        Destroy(gameObject);
    }

    public void ConfigureBehavior(FogEnemyBehaviorType newBehavior)
    {
        behaviorType = newBehavior;
        ResetHitAndRunState();
    }

    void MoveByBehavior(FogSystem fogSystem, bool lanternRepelsEnemy)
    {
        if (behaviorType == FogEnemyBehaviorType.HitAndRun)
        {
            MoveHitAndRun(fogSystem, lanternRepelsEnemy);
            return;
        }

        ResetHitAndRunState();
        MoveTowardsPlayer(fogSystem, lanternRepelsEnemy);
    }

    void MoveHitAndRun(FogSystem fogSystem, bool lanternRepelsEnemy)
    {
        if (!TryGetPlayer(out Transform playerTransform))
            return;

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.z = 0f;
        float retreatDistance = Mathf.Max(0.1f, hitAndRunRetreatDistance);
        float retreatDistanceSqr = retreatDistance * retreatDistance;

        if (!hitAndRunRetreating && toPlayer.sqrMagnitude > retreatDistanceSqr)
        {
            hitAndRunTimer = 0f;
            MoveTowardsPlayer(fogSystem, lanternRepelsEnemy);
            return;
        }

        if (!hitAndRunRetreating)
        {
            hitAndRunTimer += Time.deltaTime;
            MoveTowardsPlayer(fogSystem, lanternRepelsEnemy);

            if (hitAndRunTimer >= Mathf.Max(0.05f, hitAndRunEngageDuration))
            {
                hitAndRunRetreating = true;
                hitAndRunTimer = 0f;
            }

            return;
        }

        hitAndRunTimer += Time.deltaTime;
        MoveAwayFromPlayer(Mathf.Max(0.1f, hitAndRunRetreatSpeedMultiplier));

        if (hitAndRunTimer >= Mathf.Max(0.05f, hitAndRunRetreatDuration))
        {
            hitAndRunRetreating = false;
            hitAndRunTimer = 0f;
        }
    }

    void ResetHitAndRunState()
    {
        hitAndRunRetreating = false;
        hitAndRunTimer = 0f;
    }

    void MoveTowardsPlayer(FogSystem fogSystem, bool lanternRepelsEnemy)
    {
        if (!TryGetPlayer(out Transform playerTransform))
            return;

        Vector3 current = transform.position;
        Vector3 destination = playerTransform.position;
        destination.z = current.z;

        Vector3 nextPosition = Vector3.MoveTowards(
            current,
            destination,
            Mathf.Max(0f, moveSpeed) * Time.deltaTime
        );

        if (lanternRepelsEnemy && fogSystem.IsPositionInLanternLight(nextPosition))
            return;

        transform.position = nextPosition;
    }

    void MoveAwayFromPlayer(float speedMultiplier = 1f)
    {
        if (!TryGetPlayer(out Transform playerTransform))
            return;

        Vector3 current = transform.position;
        Vector3 away = current - playerTransform.position;
        away.z = 0f;
        if (away.sqrMagnitude <= 0.0001f)
            away = Vector3.right;

        Vector3 nextPosition = current
            + away.normalized * Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, speedMultiplier) * Time.deltaTime;
        nextPosition.z = current.z;
        transform.position = nextPosition;
    }

    bool TryGetPlayer(out Transform playerTransform)
    {
        if (target != null)
        {
            playerTransform = target;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            target = playerObject.transform;

        playerTransform = target;
        return playerTransform != null;
    }

    bool IsLanternRepelActive()
    {
        if (!TryGetPlayer(out Transform playerTransform))
            return false;

        PlayerFogPressure playerFogPressure = playerTransform.GetComponentInParent<PlayerFogPressure>();
        if (playerFogPressure == null)
            return true;

        return playerFogPressure.FogPressure < Mathf.Max(0f, lanternProtectionBreakPressure);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || other == null)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
            return;

        if (contactDamage <= 0)
            return;

        if (Time.time < nextContactDamageTime)
            return;

        nextContactDamageTime = Time.time + Mathf.Max(0.01f, contactDamageInterval);
        playerHealth.TakeDamage(contactDamage);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
            return;

        if (other.GetComponentInParent<PlayerHealth>() != null)
            nextContactDamageTime = Time.time;
    }
}
