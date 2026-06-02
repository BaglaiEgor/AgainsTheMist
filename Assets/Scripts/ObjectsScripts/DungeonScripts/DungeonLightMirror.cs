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

    public DungeonLightMirrorType MirrorType => mirrorType;

    private void Awake()
    {
        if (configureRigidbodyOnAwake)
            ConfigureRigidbody();
    }

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
