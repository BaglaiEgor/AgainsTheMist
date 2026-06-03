using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DungeonPushBox : MonoBehaviour
{
    [Header("Push Box")]
    [SerializeField] private bool configureRigidbodyOnAwake = true;

    private void Awake()
    {
        if (configureRigidbodyOnAwake)
            ConfigureRigidbody();
    }

    private void FixedUpdate()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.rotation = 0f;
        rb.angularVelocity = 0f;
    }

    private void Reset()
    {
        ConfigureRigidbody();
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
