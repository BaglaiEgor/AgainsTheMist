using DG.Tweening;
using UnityEngine;

public enum DungeonLightMirrorType
{
    Slash,
    Backslash
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DungeonLightMirror : MonoBehaviour
{
    [Header("Mirror")]
    [SerializeField] private DungeonLightMirrorType mirrorType = DungeonLightMirrorType.Slash;
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private Transform rotationCenterOverride;
    [Min(0.01f)] [SerializeField] private float rotateTweenDuration = 0.16f;

    [Header("Reflection Trigger")]
    [SerializeField] private Collider2D reflectionTriggerCollider;
    [SerializeField] private bool autoCreateReflectionTrigger = true;
    [SerializeField] private Vector2 reflectionTriggerSize = new Vector2(0.22f, 0.9f);
    [SerializeField] private Vector2 reflectionTriggerOffset = Vector2.zero;

    public string InteractLabel => "Повернуть зеркало";
    public DungeonLightMirrorType MirrorType => mirrorType;

    private void Awake()
    {
        if (configureRigidbodyOnAwake)
            ConfigureRigidbody();

        EnsureReflectionTrigger();
        ApplyVisualDirection();
    }

    private Tween rotateTween;

    private void Reset()
    {
        ConfigureRigidbody();
    }

    public Vector2 Reflect(Vector2 direction)
    {
        direction = SnapDirection(direction);

        if (mirrorType == DungeonLightMirrorType.Slash)
            return new Vector2(-direction.y, -direction.x);

        return new Vector2(direction.y, direction.x);
    }

    public bool CanReflectFrom(Collider2D hitCollider)
    {
        EnsureReflectionTrigger();
        if (hitCollider == null || !hitCollider.isTrigger)
            return false;

        if (hitCollider == reflectionTriggerCollider)
            return true;

        return hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform);
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return true;

        return Vector2.Distance(interactor.position, transform.position) <= interactDistance;
    }

    public bool TryToggleDirection(Transform interactor)
    {
        if (!CanInteract(interactor))
            return false;

        mirrorType = mirrorType == DungeonLightMirrorType.Slash
            ? DungeonLightMirrorType.Backslash
            : DungeonLightMirrorType.Slash;

        ApplyVisualDirection(true);
        return true;
    }

    private void ApplyVisualDirection(bool animated = false)
    {
        Vector3 centerBeforeRotation = GetRotationCenterWorld();
        float targetAngle = mirrorType == DungeonLightMirrorType.Slash ? 0f : 90f;

        rotateTween?.Kill();
        if (animated && Application.isPlaying)
        {
            rotateTween = transform
                .DORotate(new Vector3(0f, 0f, targetAngle), Mathf.Max(0.01f, rotateTweenDuration), RotateMode.Fast)
                .SetEase(Ease.OutBack)
                .OnUpdate(SyncRigidbodyToTransform)
                .OnComplete(() =>
                {
                    rotateTween = null;
                    ApplyVisualDirection(false);
                });
            return;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

        Vector3 centerAfterRotation = GetRotationCenterWorld();
        transform.position += centerBeforeRotation - centerAfterRotation;

        SyncRigidbodyToTransform();
    }

    private void SyncRigidbodyToTransform()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.position = transform.position;
        rb.rotation = transform.eulerAngles.z;
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void OnDisable()
    {
        rotateTween?.Kill();
        rotateTween = null;
    }

    private Vector3 GetRotationCenterWorld()
    {
        if (rotationCenterOverride != null)
            return rotationCenterOverride.position;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds.center;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            return collider.bounds.center;

        return transform.position;
    }

    private void EnsureReflectionTrigger()
    {
        if (reflectionTriggerCollider != null)
        {
            reflectionTriggerCollider.isTrigger = true;
            return;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                reflectionTriggerCollider = colliders[i];
                return;
            }
        }

        if (!autoCreateReflectionTrigger)
            return;

        GameObject triggerObject = new GameObject("MirrorReflectionTrigger");
        triggerObject.transform.SetParent(transform, false);

        BoxCollider2D triggerCollider = triggerObject.AddComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = reflectionTriggerSize;
        triggerCollider.offset = reflectionTriggerOffset;
        reflectionTriggerCollider = triggerCollider;
    }

    private static Vector2 SnapDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2(Mathf.Sign(direction.x == 0f ? 1f : direction.x), 0f);

        return new Vector2(0f, Mathf.Sign(direction.y == 0f ? 1f : direction.y));
    }

    private void ConfigureRigidbody()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }
}
