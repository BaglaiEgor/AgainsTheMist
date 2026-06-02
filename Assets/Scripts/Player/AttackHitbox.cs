using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 0.15f;
    [SerializeField] private bool animateSwing = true;
    [SerializeField] private float rotationOffset = -45f;

    private int damage;
    private IDamageable ownerDamageable;
    private readonly HashSet<IDamageable> damagedObjects = new();

    private bool swingConfigured;
    private Vector3 swingPivot;
    private float swingRadius;
    private float swingStartAngle;
    private float swingEndAngle;
    private float swingDuration;
    private float swingElapsed;

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

    public void ConfigureSwing(Vector2 direction, Vector3 pivot, float radius, float arcDegrees, float duration)
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
        }
    }

    void Update()
    {
        if (!animateSwing || !swingConfigured)
            return;

        swingElapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(swingElapsed / swingDuration);
        float eased = 1f - Mathf.Pow(1f - normalized, 3f);

        ApplySwingPose(eased);
    }

    void ApplySwingPose(float t)
    {
        float angle = Mathf.Lerp(swingStartAngle, swingEndAngle, t);
        float rad = angle * Mathf.Deg2Rad;
        float distance = Mathf.Lerp(0f, swingRadius, Mathf.Clamp01(t * 2f));

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * distance;
        transform.position = swingPivot + offset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
