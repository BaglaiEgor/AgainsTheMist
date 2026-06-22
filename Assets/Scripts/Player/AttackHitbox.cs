using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 0.15f;
    [SerializeField] private bool animateSwing = true;
    [SerializeField] private float rotationOffset = -45f;
    [SerializeField] private float rotationYOffset;
    [SerializeField] private bool preservePrefabRotation;
    [SerializeField] private bool flipVisualOnLeft = true;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private bool useLeftVisualRotation;
    [SerializeField] private Vector3 leftVisualRotation;

    private int damage;
    private IDamageable ownerDamageable;
    private readonly HashSet<IDamageable> damagedObjects = new();

    private bool swingConfigured;
    private Transform swingPivot;
    private float swingRadius;
    private float swingStartAngle;
    private float swingEndAngle;
    private float swingDuration;
    private float swingElapsed;
    private bool mirrorVisual;
    private bool swingLeft;
    private Quaternion prefabRotation;
    private Quaternion defaultVisualRotation;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void SetDamage(int value)
    {
        damage = value;
    }

    public void SetOwner(IDamageable owner)
    {
        ownerDamageable = owner;
    }

    public void ConfigureSwing(Vector2 direction, Transform pivot, float radius, float arcDegrees, float duration)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = Vector2.right;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float arc = Mathf.Clamp(arcDegrees, 10f, 220f);
        bool clockwise = direction.x >= 0f && direction.y >= 0f;

        swingPivot = pivot;
        swingRadius = Mathf.Max(0.05f, radius);
        swingStartAngle = baseAngle + (clockwise ? arc : -arc) * 0.5f;
        swingEndAngle = baseAngle - (clockwise ? arc : -arc) * 0.5f;
        swingDuration = Mathf.Max(0.01f, duration);
        swingElapsed = 0f;
        swingConfigured = true;
        swingLeft = direction.x < 0f;
        mirrorVisual = flipVisualOnLeft && swingLeft;
        prefabRotation = transform.rotation;

        if (visualTransform != null)
        {
            defaultVisualRotation = visualTransform.localRotation;
            visualTransform.localRotation = useLeftVisualRotation && swingLeft
                ? Quaternion.Euler(leftVisualRotation)
                : defaultVisualRotation;
        }

        SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null)
            visual.flipX = mirrorVisual;

        if (transform.parent != null)
        {
            Vector3 scale = transform.localScale;
            float parentScaleX = transform.parent.lossyScale.x;
            scale.x = Mathf.Abs(scale.x) * (parentScaleX < 0f ? -1f : 1f);
            transform.localScale = scale;
        }

        lifetime = swingDuration;
        ApplySwingPose(0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<WorldObject>() != null)
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            if (ownerDamageable != null && ReferenceEquals(damageable, ownerDamageable))
                return;

            if (damagedObjects.Contains(damageable))
                return;

            damagedObjects.Add(damageable);
            damageable.TakeDamage(damage);
            if (damage > 0)
                CameraShakeController.ShakeMain(0.08f, 0.035f);
        }
    }

    void Update()
    {
        if (!animateSwing || !swingConfigured)
            return;

        swingElapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(swingElapsed / swingDuration);
        float eased = Mathf.SmoothStep(0f, 1f, normalized);

        ApplySwingPose(eased);
    }

    void ApplySwingPose(float t)
    {
        float angle = Mathf.Lerp(swingStartAngle, swingEndAngle, t);
        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * swingRadius;
        float visualRotationOffset = swingLeft ? -180f - rotationOffset : rotationOffset;
        Vector3 pivotPosition = swingPivot != null ? swingPivot.position : transform.position;
        Quaternion swingRotation = Quaternion.Euler(0f, rotationYOffset, angle + visualRotationOffset);

        transform.position = pivotPosition + offset;
        transform.rotation = preservePrefabRotation ? swingRotation * prefabRotation : swingRotation;
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
