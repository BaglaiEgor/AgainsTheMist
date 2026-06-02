using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(LineRenderer))]
public class DungeonLightSource : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Transform lightOrigin;
    [SerializeField] private LayerMask raycastMask = ~0;
    [Min(0.5f)] [SerializeField] private float maxDistance = 12f;
    [Min(0)] [SerializeField] private int maxBounces = 6;
    [Min(0.001f)] [SerializeField] private float rayOffset = 0.03f;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private bool configureRigidbodyOnAwake = true;

    [Header("Visual")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private Color lineColor = new Color(1f, 0.88f, 0.2f, 1f);

    private readonly List<Vector3> points = new();

    public string InteractLabel => "\u041f\u043e\u0432\u0435\u0440\u043d\u0443\u0442\u044c";

    private void Awake()
    {
        if (lightOrigin == null)
            lightOrigin = transform;

        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (configureRigidbodyOnAwake)
            ConfigureRigidbody();

        ConfigureLineRenderer();
    }

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
        ConfigureRigidbody();
        ConfigureLineRenderer();
    }

    private void Update()
    {
        CastLight();
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return true;

        return Vector2.Distance(interactor.position, transform.position) <= interactDistance;
    }

    public bool TryRotate(Transform interactor)
    {
        if (!CanInteract(interactor))
            return false;

        float nextAngle = Mathf.Round((transform.eulerAngles.z - 90f) / 90f) * 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.rotation = nextAngle;
            rb.angularVelocity = 0f;
        }

        return true;
    }

    private void CastLight()
    {
        points.Clear();

        Vector2 origin = lightOrigin != null ? lightOrigin.position : transform.position;
        Vector2 direction = SnapDirection(lightOrigin != null ? lightOrigin.right : transform.right);
        points.Add(origin);

        for (int bounce = 0; bounce <= maxBounces; bounce++)
        {
            RaycastHit2D hit = GetFirstValidHit(origin + direction * rayOffset, direction);
            if (hit.collider == null)
            {
                points.Add(origin + direction * maxDistance);
                break;
            }

            points.Add(hit.point);

            DungeonLightReceiver receiver = hit.collider.GetComponentInParent<DungeonLightReceiver>();
            if (receiver != null)
                receiver.ReceiveLight();

            DungeonLightMirror mirror = hit.collider.GetComponentInParent<DungeonLightMirror>();
            if (mirror == null)
                break;

            origin = hit.point;
            direction = mirror.Reflect(direction);
        }

        RefreshLineRenderer();
    }

    private void RefreshLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            lineRenderer.SetPosition(i, points[i]);
    }

    private RaycastHit2D GetFirstValidHit(Vector2 origin, Vector2 direction)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, maxDistance, raycastMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            return hits[i];
        }

        return default;
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

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 0;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                lineRenderer.sharedMaterial = new Material(shader);
        }
    }
}
