using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DungeonLightSource : MonoBehaviour
{
    [Header("Ray")]
    [SerializeField] private Transform lightOrigin;
    [SerializeField] private LayerMask raycastMask = ~0;
    [Min(0.5f)] [SerializeField] private float maxDistance = 12f;
    [Min(0)] [SerializeField] private int maxBounces = 6;
    [Min(0.001f)] [SerializeField] private float rayOffset = 0.05f;
    [Min(0f)] [SerializeField] private float mirrorVisualGap = 0f;

    [Header("Beam Light2D")]
    [SerializeField] private Light2D beamTemplateLight;
    [SerializeField] private Color beamColor = new Color(0.89f, 0.52f, 0.35f, 1f);
    [Min(0f)] [SerializeField] private float beamIntensity = 0.75f;
    [InspectorName("Inner Width")] [Min(0.05f)] [SerializeField] private float beamWidth = 0.15f;
    [InspectorName("Outer Width")] [Min(0.05f)] [SerializeField] private float beamOuterWidth = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float beamFalloffIntensity = 0.75f;
    [Min(0f)] [SerializeField] private float beamVolumeIntensity = 0.25f;
    [SerializeField] private bool beamVolumetricEnabled = true;
    [Min(0f)] [SerializeField] private float beamShadowIntensity = 0f;
    [SerializeField] private int beamBlendStyleIndex = 0;
    [SerializeField] private int beamLightOrder = 0;

    [Header("Stylized Beam Visual")]
    [SerializeField] private bool enableStylizedBeamVisual = true;
    [Min(0.01f)] [SerializeField] private float beamVisualOuterWidth = 0.22f;
    [Min(0.005f)] [SerializeField] private float beamVisualCoreWidth = 0.08f;
    [Min(0.1f)] [SerializeField] private float beamVisualPulseSpeed = 8f;
    [SerializeField] private Color beamVisualOuterColor = new Color(1f, 0.45f, 0.12f, 0.62f);
    [SerializeField] private Color beamVisualCoreColor = new Color(1f, 0.88f, 0.58f, 0.82f);
    [SerializeField] private int beamVisualSortingOrderOffset = 8;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private Transform rotationCenterOverride;

    private struct BeamSegment
    {
        public Vector3 start;
        public Vector3 end;
    }

    private readonly List<BeamSegment> beamSegments = new();
    private readonly List<Light2D> beamLights = new();
    private readonly List<DungeonStylizedLineVisual> beamVisuals = new();
    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[32];
    private SpriteRenderer cachedSourceRenderer;

    public string InteractLabel => "\u041f\u043e\u0432\u0435\u0440\u043d\u0443\u0442\u044c";

    private void Awake()
    {
        if (lightOrigin == null)
            lightOrigin = transform;

        cachedSourceRenderer = GetComponentInChildren<SpriteRenderer>();
        EnsureBeamTemplate();

        if (configureRigidbodyOnAwake)
            ConfigureRigidbody();
    }

    private void Reset()
    {
        ConfigureRigidbody();
        EnsureBeamTemplate();
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

        Vector3 centerBeforeRotation = GetRotationCenterWorld();
        float nextAngle = Mathf.Round((transform.eulerAngles.z - 90f) / 90f) * 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);

        Vector3 centerAfterRotation = GetRotationCenterWorld();
        transform.position += centerBeforeRotation - centerAfterRotation;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = nextAngle;
            if (rb.bodyType != RigidbodyType2D.Static)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        return true;
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

    private void CastLight()
    {
        beamSegments.Clear();

        Vector2 origin = lightOrigin != null ? lightOrigin.position : transform.position;
        Vector2 direction = SnapDirection(lightOrigin != null ? lightOrigin.right : transform.right);
        Collider2D previousMirrorTrigger = null;

        for (int bounce = 0; bounce <= maxBounces; bounce++)
        {
            RaycastHit2D hit = GetFirstValidHit(origin + direction * rayOffset, direction, previousMirrorTrigger);
            if (hit.collider == null)
            {
                AddBeamSegment(origin, origin + direction * maxDistance);
                break;
            }

            DungeonLightReceiver receiver = hit.collider.GetComponentInParent<DungeonLightReceiver>();
            if (receiver != null)
            {
                receiver.ReceiveLight();
                AddBeamSegment(origin, hit.point);
                break;
            }

            DungeonLightMirror mirror = hit.collider.GetComponentInParent<DungeonLightMirror>();
            if (mirror == null || !mirror.CanReflectFrom(hit.collider))
            {
                AddBeamSegment(origin, hit.point);
                break;
            }

            Vector2 reflectedDirection = mirror.Reflect(direction);
            AddBeamSegment(origin, hit.point);

            origin = hit.point + reflectedDirection * Mathf.Max(rayOffset, mirrorVisualGap);
            direction = reflectedDirection;
            previousMirrorTrigger = hit.collider;
        }

        RefreshBeamLights();
    }

    private void AddBeamSegment(Vector3 start, Vector3 end)
    {
        beamSegments.Add(new BeamSegment
        {
            start = start,
            end = end
        });
    }

    private void RefreshBeamLights()
    {
        EnsureBeamTemplate();

        if (beamSegments.Count == 0)
        {
            SetActiveBeamCount(0);
            SetActiveBeamVisualCount(0);
            return;
        }

        int usedCount = 0;
        for (int i = 0; i < beamSegments.Count; i++)
        {
            Vector3 start = beamSegments[i].start;
            Vector3 end = beamSegments[i].end;
            Vector3 segment = end - start;
            float distance = segment.magnitude;
            if (distance <= 0.01f)
                continue;

            Light2D beamLight = GetBeamLight(usedCount);
            Vector3 segmentDirection = segment / distance;
            float angle = Mathf.Atan2(segmentDirection.y, segmentDirection.x) * Mathf.Rad2Deg;

            beamLight.transform.position = start;
            beamLight.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            ApplyBeamSettings(beamLight);
            beamLight.SetShapePath(CreateBeamShapePath(distance));
            beamLight.gameObject.SetActive(true);

            if (enableStylizedBeamVisual)
            {
                DungeonStylizedLineVisual beamVisual = GetBeamVisual(usedCount);
                beamVisual.Show(start, end, Mathf.PingPong(Time.time * 0.7f, 1f), false);
            }

            usedCount++;
        }

        SetActiveBeamCount(usedCount);
        SetActiveBeamVisualCount(enableStylizedBeamVisual ? usedCount : 0);
    }

    private Light2D GetBeamLight(int index)
    {
        while (beamLights.Count <= index)
        {
            GameObject lightObject = new GameObject($"BeamLight2D_{beamLights.Count}");
            lightObject.transform.SetParent(transform, true);

            Light2D beamLight = lightObject.AddComponent<Light2D>();
            beamLight.lightType = Light2D.LightType.Freeform;
            beamLights.Add(beamLight);
        }

        return beamLights[index];
    }

    private DungeonStylizedLineVisual GetBeamVisual(int index)
    {
        while (beamVisuals.Count <= index)
        {
            GameObject visualObject = new GameObject($"BeamStylizedVisual_{beamVisuals.Count}");
            visualObject.transform.SetParent(transform, true);

            DungeonStylizedLineVisual beamVisual = visualObject.AddComponent<DungeonStylizedLineVisual>();
            beamVisual.Configure(
                beamVisualOuterWidth,
                beamVisualCoreWidth,
                beamVisualPulseSpeed,
                beamVisualOuterColor,
                beamVisualCoreColor,
                beamVisualOuterColor,
                beamVisualCoreColor
            );
            beamVisual.ApplySorting(cachedSourceRenderer, beamVisualSortingOrderOffset);
            beamVisuals.Add(beamVisual);
        }

        return beamVisuals[index];
    }

    private void ApplyBeamSettings(Light2D beamLight)
    {
        beamLight.lightType = Light2D.LightType.Freeform;
        beamLight.enabled = true;

        if (beamTemplateLight == null)
        {
            beamLight.color = beamColor;
            beamLight.intensity = beamIntensity;
            beamLight.shapeLightFalloffSize = GetBeamFalloffSize();
            beamLight.falloffIntensity = beamFalloffIntensity;
            beamLight.volumeIntensity = beamVolumeIntensity;
            beamLight.volumetricEnabled = beamVolumetricEnabled && beamVolumeIntensity > 0f;
            beamLight.shadowIntensity = beamShadowIntensity;
            beamLight.blendStyleIndex = beamBlendStyleIndex;
            beamLight.lightOrder = beamLightOrder;
            return;
        }

        beamLight.color = beamTemplateLight.color;
        beamLight.intensity = beamTemplateLight.intensity;
        beamLight.shapeLightFalloffSize = beamTemplateLight.shapeLightFalloffSize;
        beamLight.falloffIntensity = beamTemplateLight.falloffIntensity;
        beamLight.volumeIntensity = beamTemplateLight.volumeIntensity;
        beamLight.volumetricEnabled = beamTemplateLight.volumetricEnabled;
        beamLight.shadowIntensity = beamTemplateLight.shadowIntensity;
        beamLight.blendStyleIndex = beamTemplateLight.blendStyleIndex;
        beamLight.lightOrder = beamTemplateLight.lightOrder;
    }

    private Vector3[] CreateBeamShapePath(float length)
    {
        float halfWidth = Mathf.Max(beamWidth, beamOuterWidth) * 0.5f;
        return new[]
        {
            new Vector3(0f, -halfWidth, 0f),
            new Vector3(length, -halfWidth, 0f),
            new Vector3(length, halfWidth, 0f),
            new Vector3(0f, halfWidth, 0f)
        };
    }

    private float GetBeamFalloffSize()
    {
        return Mathf.Max(0f, (Mathf.Max(beamWidth, beamOuterWidth) - beamWidth) * 0.5f);
    }

    private void SetActiveBeamCount(int activeCount)
    {
        for (int i = 0; i < beamLights.Count; i++)
            beamLights[i].gameObject.SetActive(i < activeCount);
    }

    private void SetActiveBeamVisualCount(int activeCount)
    {
        for (int i = 0; i < beamVisuals.Count; i++)
        {
            if (i < activeCount)
                continue;

            beamVisuals[i].Hide();
        }
    }

    private RaycastHit2D GetFirstValidHit(Vector2 origin, Vector2 direction, Collider2D ignoredCollider)
    {
        ContactFilter2D filter = CreateRaycastFilter();
        int hitCount = Physics2D.Raycast(origin, direction, filter, raycastHits, maxDistance);
        RaycastHit2D closestHit = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            if (hitCollider == ignoredCollider)
                continue;

            if (hitCollider is TilemapCollider2D)
                continue;

            DungeonLightMirror mirror = hitCollider.GetComponentInParent<DungeonLightMirror>();
            if (mirror != null)
            {
                if (!mirror.CanReflectFrom(hitCollider))
                    continue;
            }

            if (raycastHits[i].distance < closestDistance)
            {
                closestDistance = raycastHits[i].distance;
                closestHit = raycastHits[i];
            }
        }

        return closestHit;
    }

    private ContactFilter2D CreateRaycastFilter()
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(raycastMask);
        return filter;
    }

    private void EnsureBeamTemplate()
    {
        if (beamTemplateLight == null)
        {
            Light2D[] lights = GetComponentsInChildren<Light2D>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && !beamLights.Contains(lights[i]))
                {
                    beamTemplateLight = lights[i];
                    break;
                }
            }
        }

        if (beamTemplateLight != null)
            beamTemplateLight.enabled = false;
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
