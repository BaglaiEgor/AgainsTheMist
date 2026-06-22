using DG.Tweening;
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
    private enum EnemyState
    {
        Normal,
        Roll,
        AttackTelegraph,
        AttackDash,
        Hit,
        Dying
    }

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

    [Header("Goblin Moves")]
    [SerializeField] private bool enableGoblinMoves;
    [SerializeField] private int lineAttackDamage = 8;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private float attackTelegraphTime = 0.45f;
    [SerializeField] private float attackDashDistance = 1.25f;
    [SerializeField] private float attackDashDuration = 0.18f;
    [SerializeField] private float attackLineWidth = 0.08f;
    [SerializeField] private Color attackLineColor = new Color(1f, 0.12f, 0.08f, 0.9f);

    [Header("Goblin Attack Visual")]
    [SerializeField] private bool enableStylizedAttackLine = true;
    [Min(0.01f)] [SerializeField] private float attackLineOuterWidth = 0.22f;
    [Min(0.005f)] [SerializeField] private float attackLineCoreWidth = 0.08f;
    [Min(0.1f)] [SerializeField] private float attackLinePulseSpeed = 10f;
    [SerializeField] private Color attackWarningOuterColor = new Color(0.55f, 0.12f, 0.82f, 0.55f);
    [SerializeField] private Color attackWarningCoreColor = new Color(0.95f, 0.55f, 1f, 0.75f);
    [SerializeField] private Color attackActiveOuterColor = new Color(1f, 0.2f, 0.95f, 0.95f);
    [SerializeField] private Color attackActiveCoreColor = new Color(1f, 0.85f, 1f, 1f);
    [Min(0f)] [SerializeField] private float attackActiveFlashTime = 0.1f;

    [SerializeField] private float approachRollChance = 0.25f;
    [SerializeField] private float approachRollCooldown = 2.2f;
    [SerializeField] private float rollDodgeChance = 0.45f;
    [SerializeField] private float rollDuration = 0.35f;
    [SerializeField] private float rollSpeedMultiplier = 2.8f;
    [SerializeField] private float rollInvulnerableTime = 0.28f;
    [SerializeField] private float hitKnockbackDistance = 0.35f;
    [SerializeField] private float hitKnockbackDuration = 0.16f;
    [SerializeField] private float hitAnimationDuration = 0.22f;
    [SerializeField] private float deathAnimationDuration = 0.75f;
    [Min(0.05f)] [SerializeField] private float simpleDeathTweenDuration = 0.18f;
    [SerializeField] private Vector3 deathPunchScale = new Vector3(0.12f, 0.12f, 0f);

    [Header("Goblin Animation")]
    [SerializeField] private bool enableSpriteAnimation;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] idleSprites;
    [SerializeField] private Sprite[] runSprites;
    [SerializeField] private Sprite[] rollSprites;
    [SerializeField] private Sprite[] hitSprites;
    [SerializeField] private Sprite[] deathSprites;
    [SerializeField] private float idleFrameTime = 0.12f;
    [SerializeField] private float runFrameTime = 0.08f;
    [SerializeField] private float rollFrameTime = 0.045f;
    [SerializeField] private float hitFrameTime = 0.07f;
    [SerializeField] private float deathFrameTime = 0.07f;

    [Header("Fog")]
    [SerializeField] private float despawnDelayInLight = 2f;
    [SerializeField] private float lanternProtectionBreakPressure = 70f;

    private Transform target;
    private Collider2D ownCollider;
    private int currentHealth;
    private float nextContactDamageTime;
    private float lightDespawnTimer;
    private bool hitAndRunRetreating;
    private float hitAndRunTimer;
    private bool isDead;
    private EnemyState state;
    private Vector3 stateStartPosition;
    private Vector3 stateEndPosition;
    private Vector3 stateDirection = Vector3.right;
    private float stateTimer;
    private float stateDuration;
    private float nextAttackTime;
    private float nextApproachRollTime;
    private bool attackDamageApplied;
    private bool stateMovementThisFrame;
    private LineRenderer attackLine;
    private Material attackLineMaterial;
    private DungeonStylizedLineVisual stylizedAttackLine;
    private float attackActiveFlashTimer;
    private Tween deathTween;
    private Sprite[] currentAnimation;
    private float currentFrameTime;
    private int animationFrame;
    private float animationTimer;

    public bool IsDead => isDead;

    void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);

        ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (isDead && state != EnemyState.Dying)
            return;

        stateMovementThisFrame = false;
        if (enableGoblinMoves && state != EnemyState.Normal)
        {
            UpdateState();
            UpdateAnimation();
            return;
        }

        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        bool inFog = fogSystem.IsPositionInEnemyFog(transform.position);
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
            UpdateAnimation();
            return;
        }

        MoveByBehavior(fogSystem, lanternRepelsEnemy);
        UpdateAnimation();
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        if (enableGoblinMoves && TryStartDodgeRoll())
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        HitFlashFeedback.PlayOn(gameObject, new Color(1f, 0.55f, 0.5f, 1f), 0.12f);
        if (currentHealth > 0)
        {
            if (enableGoblinMoves)
                StartHitReaction();

            return;
        }

        Die();
    }

    void Die()
    {
        isDead = true;
        HideAttackLine();

        if (!enableGoblinMoves)
        {
            if (ownCollider != null)
                ownCollider.enabled = false;

            DropResources();
            PlaySimpleDeathFeedback();
            return;
        }

        if (ownCollider != null)
            ownCollider.enabled = false;

        state = EnemyState.Dying;
        stateTimer = 0f;
        stateDuration = Mathf.Max(0.05f, deathAnimationDuration);
        PlayAnimation(deathSprites, deathFrameTime);
        transform.DOPunchScale(deathPunchScale, 0.18f, 6, 0.45f).SetEase(Ease.OutQuad);
    }

    void PlaySimpleDeathFeedback()
    {
        if (deathTween != null)
            deathTween.Kill();

        float duration = Mathf.Max(0.05f, simpleDeathTweenDuration);
        Sequence sequence = DOTween.Sequence();
        sequence.Join(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));

        SpriteRenderer renderer = spriteRenderer != null ? spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            sequence.Join(renderer.DOFade(0f, duration));

        deathTween = sequence.OnComplete(() =>
        {
            deathTween = null;
            if (gameObject != null)
                Destroy(gameObject);
        });
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
            if (TryStartApproachRoll(toPlayer))
                return;

            MoveTowardsPlayer(fogSystem, lanternRepelsEnemy);
            return;
        }

        if (enableGoblinMoves && TryStartAttack(toPlayer))
            return;

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

        if (!fogSystem.IsPositionInEnemyFog(nextPosition))
            return;

        if (lanternRepelsEnemy && fogSystem.IsPositionInLanternLight(nextPosition))
            return;

        transform.position = nextPosition;
        stateMovementThisFrame = (nextPosition - current).sqrMagnitude > 0.00001f;
        FaceDirection(nextPosition - current);
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
        stateMovementThisFrame = (nextPosition - current).sqrMagnitude > 0.00001f;
        FaceDirection(nextPosition - current);
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

        if (enableGoblinMoves)
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

    bool TryStartAttack(Vector3 toPlayer)
    {
        if (!enableGoblinMoves || Time.time < nextAttackTime || toPlayer.sqrMagnitude <= 0.0001f)
            return false;

        float range = Mathf.Max(0.1f, attackRange);
        if (toPlayer.sqrMagnitude > range * range)
            return false;

        state = EnemyState.AttackTelegraph;
        stateTimer = 0f;
        stateDuration = Mathf.Max(0.05f, attackTelegraphTime);
        stateDirection = toPlayer.normalized;
        attackDamageApplied = false;
        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
        ResetHitAndRunState();
        ShowAttackLine();
        PlayAnimation(idleSprites, idleFrameTime);
        return true;
    }

    bool TryStartApproachRoll(Vector3 toPlayer)
    {
        if (!enableGoblinMoves || Time.time < nextApproachRollTime)
            return false;

        if (toPlayer.sqrMagnitude <= attackRange * attackRange)
            return false;

        if (Random.value > Mathf.Clamp01(approachRollChance))
            return false;

        nextApproachRollTime = Time.time + Mathf.Max(0.1f, approachRollCooldown);
        StartRoll(toPlayer.normalized);
        return true;
    }

    bool TryStartDodgeRoll()
    {
        if (!enableGoblinMoves || state == EnemyState.Dying || state == EnemyState.Roll)
            return false;

        if (Random.value > Mathf.Clamp01(rollDodgeChance))
            return false;

        Vector3 direction = Vector3.right;
        if (TryGetPlayer(out Transform playerTransform))
        {
            direction = transform.position - playerTransform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.right;
        }

        StartRoll(direction.normalized);
        return true;
    }

    void StartRoll(Vector3 direction)
    {
        HideAttackLine();
        state = EnemyState.Roll;
        stateTimer = 0f;
        stateDuration = Mathf.Max(0.05f, rollDuration);
        stateDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        stateStartPosition = transform.position;
        float distance = Mathf.Max(0f, moveSpeed) * Mathf.Max(0.1f, rollSpeedMultiplier) * stateDuration;
        stateEndPosition = stateStartPosition + stateDirection * distance;
        ResetHitAndRunState();
        FaceDirection(stateDirection);
        SetColliderEnabled(false);
        PlayAnimation(rollSprites, rollFrameTime);
    }

    void StartHitReaction()
    {
        HideAttackLine();
        state = EnemyState.Hit;
        stateTimer = 0f;
        stateDuration = Mathf.Max(0.05f, Mathf.Max(hitAnimationDuration, hitKnockbackDuration));
        stateDirection = Vector3.right;

        if (TryGetPlayer(out Transform playerTransform))
        {
            stateDirection = transform.position - playerTransform.position;
            stateDirection.z = 0f;
            if (stateDirection.sqrMagnitude <= 0.0001f)
                stateDirection = Vector3.right;
        }

        stateDirection.Normalize();
        stateStartPosition = transform.position;
        stateEndPosition = stateStartPosition + stateDirection * Mathf.Max(0f, hitKnockbackDistance);
        ResetHitAndRunState();
        FaceDirection(-stateDirection);
        PlayAnimation(hitSprites, hitFrameTime);
    }

    void UpdateState()
    {
        stateTimer += Time.deltaTime;

        if (state == EnemyState.Roll)
        {
            float normalized = Mathf.Clamp01(stateTimer / stateDuration);
            Vector3 nextPosition = Vector3.Lerp(stateStartPosition, stateEndPosition, normalized);
            if (!CanMoveToEnemyFog(nextPosition))
            {
                FinishSpecialState();
                return;
            }

            transform.position = nextPosition;
            stateMovementThisFrame = true;

            if (stateTimer >= Mathf.Max(0f, rollInvulnerableTime))
                SetColliderEnabled(true);

            if (normalized >= 1f)
                FinishSpecialState();

            return;
        }

        if (state == EnemyState.AttackTelegraph)
        {
            UpdateAttackLine();
            FaceDirection(stateDirection);

            if (stateTimer >= stateDuration)
                StartAttackDash();

            return;
        }

        if (state == EnemyState.AttackDash)
        {
            float normalized = Mathf.Clamp01(stateTimer / stateDuration);
            Vector3 nextPosition = Vector3.Lerp(stateStartPosition, stateEndPosition, normalized);
            if (!CanMoveToEnemyFog(nextPosition))
            {
                FinishSpecialState();
                return;
            }

            transform.position = nextPosition;
            stateMovementThisFrame = true;
            UpdateActiveAttackLine();
            TryApplyLineAttackDamage();

            if (normalized >= 1f)
                FinishSpecialState();

            return;
        }

        if (state == EnemyState.Hit)
        {
            float knockbackTime = Mathf.Max(0.01f, hitKnockbackDuration);
            float normalized = Mathf.Clamp01(stateTimer / knockbackTime);
            Vector3 nextPosition = Vector3.Lerp(stateStartPosition, stateEndPosition, normalized);
            if (CanMoveToEnemyFog(nextPosition))
                transform.position = nextPosition;

            stateMovementThisFrame = normalized < 1f;

            if (stateTimer >= stateDuration)
                FinishSpecialState();

            return;
        }

        if (state == EnemyState.Dying && stateTimer >= stateDuration)
        {
            DropResources();
            Destroy(gameObject);
        }
    }

    void StartAttackDash()
    {
        state = EnemyState.AttackDash;
        stateTimer = 0f;
        stateDuration = Mathf.Max(0.03f, attackDashDuration);
        stateStartPosition = transform.position;
        stateEndPosition = stateStartPosition + stateDirection * GetAttackLineDistance();
        attackActiveFlashTimer = Mathf.Max(0f, attackActiveFlashTime);
        ShowAttackLine(true);
        FaceDirection(stateDirection);
        PlayAnimation(rollSprites, rollFrameTime);
    }

    void FinishSpecialState()
    {
        HideAttackLine();
        SetColliderEnabled(true);
        state = EnemyState.Normal;
        stateTimer = 0f;
        attackDamageApplied = false;
    }

    void TryApplyLineAttackDamage()
    {
        if (attackDamageApplied || !TryGetPlayer(out Transform playerTransform))
            return;

        PlayerHealth playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
            return;

        if (IsPointNearSegment(playerTransform.position, stateStartPosition, stateEndPosition, Mathf.Max(0.05f, attackLineWidth) * 3f))
        {
            attackDamageApplied = true;
            playerHealth.TakeDamage(Mathf.Max(0, lineAttackDamage));
        }
    }

    bool IsPointNearSegment(Vector3 point, Vector3 start, Vector3 end, float distance)
    {
        Vector3 segment = end - start;
        segment.z = 0f;
        Vector3 toPoint = point - start;
        toPoint.z = 0f;

        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= 0.0001f)
            return toPoint.sqrMagnitude <= distance * distance;

        float t = Mathf.Clamp01(Vector3.Dot(toPoint, segment) / segmentLengthSqr);
        Vector3 closest = start + segment * t;
        return (point - closest).sqrMagnitude <= distance * distance;
    }

    void ShowAttackLine()
    {
        ShowAttackLine(false);
    }

    void ShowAttackLine(bool active)
    {
        EnsureAttackLine();
        if (!HasAttackLine())
            return;

        if (attackLine != null)
            attackLine.enabled = !enableStylizedAttackLine;

        UpdateAttackLine(active);
    }

    void UpdateAttackLine()
    {
        UpdateAttackLine(false);
    }

    void UpdateAttackLine(bool active)
    {
        EnsureAttackLine();
        if (!HasAttackLine())
            return;

        Vector3 start = transform.position;
        Vector3 end = start + stateDirection * GetAttackLineDistance();
        start.z = transform.position.z;
        end.z = transform.position.z;

        if (enableStylizedAttackLine && stylizedAttackLine != null)
        {
            float progress = active
                ? 1f - Mathf.Clamp01(attackActiveFlashTimer / Mathf.Max(0.01f, attackActiveFlashTime))
                : Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, stateDuration));

            stylizedAttackLine.Show(start, end, progress, active);
            return;
        }

        if (attackLine != null)
        {
            attackLine.SetPosition(0, start);
            attackLine.SetPosition(1, end);
        }
    }

    void HideAttackLine()
    {
        if (attackLine != null)
            attackLine.enabled = false;

        if (stylizedAttackLine != null)
            stylizedAttackLine.Hide();
    }

    void UpdateActiveAttackLine()
    {
        if (attackActiveFlashTimer <= 0f)
        {
            HideAttackLine();
            return;
        }

        attackActiveFlashTimer = Mathf.Max(0f, attackActiveFlashTimer - Time.deltaTime);
        UpdateAttackLine(true);
    }

    float GetAttackLineDistance()
    {
        return Mathf.Max(0.1f, attackDashDistance, attackRange);
    }

    void EnsureAttackLine()
    {
        if (enableStylizedAttackLine)
        {
            EnsureStylizedAttackLine();
            return;
        }

        if (attackLine != null)
            return;

        GameObject lineObject = new GameObject("Attack Warning Line");
        lineObject.transform.SetParent(transform, false);
        attackLine = lineObject.AddComponent<LineRenderer>();
        attackLine.positionCount = 2;
        attackLine.useWorldSpace = true;
        attackLine.startWidth = Mathf.Max(0.01f, attackLineWidth);
        attackLine.endWidth = Mathf.Max(0.01f, attackLineWidth);
        attackLine.startColor = attackLineColor;
        attackLine.endColor = attackLineColor;
        attackLine.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 12;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            attackLineMaterial = new Material(shader);
            attackLine.material = attackLineMaterial;
        }

        attackLine.enabled = false;
    }

    void EnsureStylizedAttackLine()
    {
        if (stylizedAttackLine != null)
            return;

        Transform existing = transform.Find("Stylized Attack Warning Line");
        GameObject lineObject = existing != null ? existing.gameObject : new GameObject("Stylized Attack Warning Line");
        lineObject.transform.SetParent(transform, false);

        stylizedAttackLine = lineObject.GetComponent<DungeonStylizedLineVisual>();
        if (stylizedAttackLine == null)
            stylizedAttackLine = lineObject.AddComponent<DungeonStylizedLineVisual>();

        stylizedAttackLine.Configure(
            attackLineOuterWidth,
            attackLineCoreWidth,
            attackLinePulseSpeed,
            attackWarningOuterColor,
            attackWarningCoreColor,
            attackActiveOuterColor,
            attackActiveCoreColor
        );
        stylizedAttackLine.SetKeepWidthStable(true);
        stylizedAttackLine.ApplySorting(spriteRenderer, -2);
        stylizedAttackLine.Hide();
    }

    bool HasAttackLine()
    {
        return enableStylizedAttackLine ? stylizedAttackLine != null : attackLine != null;
    }

    void SetColliderEnabled(bool enabled)
    {
        if (ownCollider != null)
            ownCollider.enabled = enabled;
    }

    bool CanMoveToEnemyFog(Vector3 position)
    {
        FogSystem fogSystem = FogSystem.Instance;
        return fogSystem == null || fogSystem.IsPositionInEnemyFog(position);
    }

    void DropResources()
    {
        ResourceDrop resourceDrop = GetComponent<ResourceDrop>();
        if (resourceDrop != null)
            resourceDrop.DropNow();
    }

    void FaceDirection(Vector3 direction)
    {
        if (spriteRenderer == null || direction.sqrMagnitude <= 0.0001f)
            return;

        spriteRenderer.flipX = direction.x < 0f;
    }

    void UpdateAnimation()
    {
        if ((!enableGoblinMoves && !enableSpriteAnimation) || spriteRenderer == null)
            return;

        if (state == EnemyState.Normal)
        {
            if (stateMovementThisFrame)
                PlayAnimation(runSprites, runFrameTime);
            else
                PlayAnimation(idleSprites, idleFrameTime);
        }

        if (currentAnimation == null || currentAnimation.Length == 0)
            return;

        animationTimer += Time.deltaTime;
        float frameTime = Mathf.Max(0.01f, currentFrameTime);
        if (animationTimer < frameTime)
            return;

        animationTimer = 0f;
        animationFrame++;

        if (animationFrame >= currentAnimation.Length)
            animationFrame = state == EnemyState.Dying || state == EnemyState.Hit || state == EnemyState.Roll
                ? currentAnimation.Length - 1
                : 0;

        spriteRenderer.sprite = currentAnimation[animationFrame];
    }

    void PlayAnimation(Sprite[] sprites, float frameTime)
    {
        if ((!enableGoblinMoves && !enableSpriteAnimation) || spriteRenderer == null || sprites == null || sprites.Length == 0)
            return;

        if (currentAnimation == sprites)
            return;

        currentAnimation = sprites;
        currentFrameTime = frameTime;
        animationFrame = 0;
        animationTimer = 0f;
        spriteRenderer.sprite = currentAnimation[0];
    }

    void OnDestroy()
    {
        deathTween?.Kill();

        if (attackLineMaterial != null)
            Destroy(attackLineMaterial);
    }
}
