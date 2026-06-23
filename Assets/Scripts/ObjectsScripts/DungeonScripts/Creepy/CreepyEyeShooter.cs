using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CreepyEyeShooter : MonoBehaviour
{
    private const string PlayerHitboxName = "HitBox";

    [Header("Refs")]
    [SerializeField] private Transform aimRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string playerTag = "Player";

    [Header("Eye Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite chargingSprite;
    [SerializeField] private Sprite firingSprite;

    [Header("Shot")]
    [Min(0)] [SerializeField] private int damage = 9;
    [Min(0.1f)] [SerializeField] private float range = 4.5f;
    [Min(0.1f)] [SerializeField] private float cooldown = 1.75f;
    [Min(0.05f)] [SerializeField] private float telegraphTime = 0.9f;
    [Min(0f)] [SerializeField] private float aimTrackTime = 0.35f;
    [Min(0.02f)] [SerializeField] private float activeBeamTime = 0.14f;
    [Min(0.01f)] [SerializeField] private float hitWidth = 0.16f;
    [SerializeField] private LayerMask playerMask = ~0;

    [Header("Beam Visual")]
    [Min(0.02f)] [SerializeField] private float beamOuterWidth = 0.22f;
    [Min(0.01f)] [SerializeField] private float beamCoreWidth = 0.08f;
    [SerializeField] private Color warningOuterColor = new Color(0.55f, 0.12f, 0.82f, 0.55f);
    [SerializeField] private Color warningCoreColor = new Color(0.95f, 0.55f, 1f, 0.75f);
    [SerializeField] private Color activeOuterColor = new Color(1f, 0.2f, 0.95f, 0.95f);
    [SerializeField] private Color activeCoreColor = new Color(1f, 0.85f, 1f, 1f);
    [Min(0.5f)] [SerializeField] private float warningPulseSpeed = 10f;
    [SerializeField] private int beamSortingOrderOffset = 8;

    private Transform player;
    private Coroutine shootRoutine;
    private Vector3 lockedDirection = Vector3.right;
    private LineRenderer outerBeamLine;
    private LineRenderer coreBeamLine;
    private Material outerBeamMaterial;
    private Material coreBeamMaterial;

    private void Awake()
    {
        if (aimRoot == null)
            aimRoot = transform;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && idleSprite == null)
            idleSprite = spriteRenderer.sprite;

        EnsureBeamLines();
        SetEyeSprite(idleSprite);
        HideBeam();
    }

    private void OnEnable()
    {
        shootRoutine = StartCoroutine(ShootLoop());
    }

    private void OnDisable()
    {
        if (shootRoutine != null)
        {
            StopCoroutine(shootRoutine);
            shootRoutine = null;
        }

        SetEyeSprite(idleSprite);
        HideBeam();
    }

    private IEnumerator ShootLoop()
    {
        WaitForSeconds waitFrame = new WaitForSeconds(0.05f);

        while (enabled)
        {
            if (!TryGetPlayer(out Transform playerTransform))
            {
                SetEyeSprite(idleSprite);
                HideBeam();
                yield return waitFrame;
                continue;
            }

            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.z = 0f;
            if (toPlayer.sqrMagnitude > range * range || toPlayer.sqrMagnitude <= 0.0001f)
            {
                FaceDirection(toPlayer);
                SetEyeSprite(idleSprite);
                HideBeam();
                yield return waitFrame;
                continue;
            }

            lockedDirection = toPlayer.normalized;
            SetEyeSprite(chargingSprite != null ? chargingSprite : idleSprite);
            ShowWarningBeam(0f);

            float timer = 0f;
            while (timer < telegraphTime)
            {
                timer += Time.deltaTime;
                float telegraphProgress = Mathf.Clamp01(timer / telegraphTime);

                if (timer <= aimTrackTime && TryGetPlayer(out playerTransform))
                {
                    Vector3 currentDirection = playerTransform.position - transform.position;
                    currentDirection.z = 0f;
                    if (currentDirection.sqrMagnitude > 0.0001f)
                        lockedDirection = currentDirection.normalized;
                }

                FaceDirection(lockedDirection);
                UpdateWarningBeam(telegraphProgress);
                yield return null;
            }

            yield return FireBeam();

            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);
        }
    }

    private IEnumerator FireBeam()
    {
        SetEyeSprite(firingSprite != null ? firingSprite : chargingSprite);
        ShowActiveBeam();
        ApplyLineDamage();

        float timer = 0f;
        while (timer < activeBeamTime)
        {
            timer += Time.deltaTime;
            UpdateActiveBeam(timer / activeBeamTime);
            yield return null;
        }

        SetEyeSprite(idleSprite);
        HideBeam();
    }

    private bool TryGetPlayer(out Transform playerTransform)
    {
        if (player != null)
        {
            playerTransform = player;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            player = playerObject.transform;

        playerTransform = player;
        return playerTransform != null;
    }

    private void ApplyLineDamage()
    {
        Vector2 origin = transform.position;
        Vector2 direction = lockedDirection;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 boxCenter = origin + direction * range * 0.5f;
        Vector2 boxSize = new Vector2(range, hitWidth * 2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, playerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!TryGetPlayerHealthFromHitbox(hits[i], out PlayerHealth playerHealth) || playerHealth.IsDead)
                continue;

            playerHealth.TakeDamage(damage);
            return;
        }
    }

    private static bool TryGetPlayerHealthFromHitbox(Collider2D other, out PlayerHealth playerHealth)
    {
        playerHealth = null;
        if (other == null || other.name != PlayerHitboxName)
            return false;

        playerHealth = other.GetComponentInParent<PlayerHealth>();
        return playerHealth != null;
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        if (spriteRenderer != null)
            spriteRenderer.flipX = direction.x < 0f;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (aimRoot != null)
            aimRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void EnsureBeamLines()
    {
        if (outerBeamLine != null && coreBeamLine != null)
            return;

        outerBeamLine = CreateBeamLine("Eye Beam Outer", beamOuterWidth, warningOuterColor, out outerBeamMaterial);
        coreBeamLine = CreateBeamLine("Eye Beam Core", beamCoreWidth, warningCoreColor, out coreBeamMaterial);
        ApplyBeamSorting(outerBeamLine);
        ApplyBeamSorting(coreBeamLine);
        HideBeam();
    }

    private LineRenderer CreateBeamLine(string objectName, float width, Color color, out Material material)
    {
        Transform existing = transform.Find(objectName);
        GameObject lineObject = existing != null ? existing.gameObject : new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
            line = lineObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.startWidth = width;
        line.endWidth = width * 0.75f;
        line.startColor = color;
        line.endColor = color;
        line.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        material = shader != null ? new Material(shader) : null;
        if (material != null)
            line.material = material;

        return line;
    }

    private void ApplyBeamSorting(LineRenderer line)
    {
        if (line == null)
            return;

        if (spriteRenderer != null)
        {
            line.sortingLayerID = spriteRenderer.sortingLayerID;
            line.sortingOrder = spriteRenderer.sortingOrder + beamSortingOrderOffset;
        }
        else
        {
            line.sortingOrder = 10 + beamSortingOrderOffset;
        }
    }

    private void UpdateBeamPositions()
    {
        if (outerBeamLine == null || coreBeamLine == null)
            return;

        Vector3 start = transform.position;
        Vector3 end = start + lockedDirection * range;
        start.z = transform.position.z;
        end.z = transform.position.z;

        outerBeamLine.SetPosition(0, start);
        outerBeamLine.SetPosition(1, end);
        coreBeamLine.SetPosition(0, start);
        coreBeamLine.SetPosition(1, end);
    }

    private void ShowWarningBeam(float progress)
    {
        EnsureBeamLines();
        outerBeamLine.enabled = true;
        coreBeamLine.enabled = true;
        UpdateBeamPositions();
        UpdateWarningBeam(progress);
    }

    private void UpdateWarningBeam(float progress)
    {
        if (outerBeamLine == null || coreBeamLine == null)
            return;

        UpdateBeamPositions();

        float pulse = 0.65f + Mathf.Sin(Time.time * warningPulseSpeed) * 0.35f;
        float ramp = Mathf.Lerp(0.5f, 1f, progress);
        float outerWidth = beamOuterWidth * Mathf.Lerp(0.75f, 1.1f, progress) * pulse;
        float coreWidth = beamCoreWidth * Mathf.Lerp(0.8f, 1.2f, progress);

        outerBeamLine.startWidth = outerWidth;
        outerBeamLine.endWidth = outerWidth * 0.7f;
        coreBeamLine.startWidth = coreWidth;
        coreBeamLine.endWidth = coreWidth * 0.65f;

        Color outer = warningOuterColor;
        outer.a = warningOuterColor.a * pulse * ramp;
        Color core = warningCoreColor;
        core.a = warningCoreColor.a * pulse * ramp;

        outerBeamLine.startColor = outer;
        outerBeamLine.endColor = outer * 0.65f;
        coreBeamLine.startColor = core;
        coreBeamLine.endColor = core * 0.55f;
    }

    private void ShowActiveBeam()
    {
        EnsureBeamLines();
        outerBeamLine.enabled = true;
        coreBeamLine.enabled = true;
        UpdateBeamPositions();
        outerBeamLine.startColor = activeOuterColor;
        outerBeamLine.endColor = activeOuterColor * 0.75f;
        coreBeamLine.startColor = activeCoreColor;
        coreBeamLine.endColor = activeCoreColor * 0.8f;
    }

    private void UpdateActiveBeam(float progress)
    {
        if (outerBeamLine == null || coreBeamLine == null)
            return;

        UpdateBeamPositions();

        float flash = 1f - progress;
        float outerWidth = Mathf.Lerp(beamOuterWidth * 1.35f, beamOuterWidth * 0.8f, progress);
        float coreWidth = Mathf.Lerp(beamCoreWidth * 1.5f, beamCoreWidth * 0.7f, progress);

        outerBeamLine.startWidth = outerWidth;
        outerBeamLine.endWidth = outerWidth * 0.75f;
        coreBeamLine.startWidth = coreWidth;
        coreBeamLine.endWidth = coreWidth * 0.7f;

        Color outer = activeOuterColor;
        outer.a = Mathf.Lerp(activeOuterColor.a * 0.35f, activeOuterColor.a, flash);
        Color core = activeCoreColor;
        core.a = Mathf.Lerp(activeCoreColor.a * 0.45f, activeCoreColor.a, flash);

        outerBeamLine.startColor = outer;
        outerBeamLine.endColor = outer * 0.7f;
        coreBeamLine.startColor = core;
        coreBeamLine.endColor = core * 0.75f;
    }

    private void HideBeam()
    {
        if (outerBeamLine != null)
            outerBeamLine.enabled = false;

        if (coreBeamLine != null)
            coreBeamLine.enabled = false;
    }

    private void SetEyeSprite(Sprite sprite)
    {
        if (spriteRenderer == null || sprite == null)
            return;

        spriteRenderer.sprite = sprite;
    }

    private void OnDestroy()
    {
        if (outerBeamMaterial != null)
            Destroy(outerBeamMaterial);

        if (coreBeamMaterial != null)
            Destroy(coreBeamMaterial);
    }
}
