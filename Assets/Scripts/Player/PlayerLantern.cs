using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class PlayerLantern : MonoBehaviour
{
    private static PlayerLantern instance;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private FogRepeller lanternRepeller;
    [SerializeField] private Transform lanternDrawPoint;
    [SerializeField] private Light2D heldLanternLight;

    [Header("Held Light2D")]
    [SerializeField] private bool autoCreateHeldLight = true;
    [SerializeField] private Color heldLightColor = new Color(0.89019614f, 0.5176471f, 0.34509805f, 1f);
    [Min(0f)] [SerializeField] private float heldLightIntensity = 0.7f;
    [Min(0.01f)] [SerializeField] private float heldLightInnerRadius = 0.15f;
    [Min(0.1f)] [SerializeField] private float heldLightVisualOuterRadius = 17f;
    [Range(0f, 1f)] [SerializeField] private float heldLightFalloffIntensity = 0.7f;
    [Min(0f)] [SerializeField] private float heldLightVolumeIntensity = 0.2f;
    [SerializeField] private bool heldLightVolumetricEnabled = true;
    [Range(0f, 1f)] [SerializeField] private float heldLightShadowIntensity = 0.001f;

    [Header("Debug")]
    [SerializeField] private bool logCharge;
    [SerializeField] private float logStep = 10f;

    private readonly Dictionary<ItemData, float> chargeByItem = new Dictionary<ItemData, float>();
    private ItemData lastLanternItem;
    private int lastLoggedBucket = -1;

    public static PlayerLantern Instance => instance;

    void Awake()
    {
        instance = this;

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        EnsureRepeller();
        EnsureHeldLight();
        SetLanternVisualActive(false);
    }

    void OnDisable()
    {
        SetLanternVisualActive(false);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static bool TryGetLanternCharge(ItemData item, out float currentCharge, out float maxCharge)
    {
        currentCharge = 0f;
        maxCharge = 0f;

        if (instance == null)
            return false;

        return instance.TryGetLanternChargeInternal(item, out currentCharge, out maxCharge);
    }

    void Update()
    {
        if (inventory == null)
            return;

        ItemData currentItem = inventory.GetCurrentItem();
        bool isLanternItem = currentItem != null &&
                             (currentItem.type == ItemType.Lantern || currentItem.isLantern);
        if (!isLanternItem)
        {
            lastLanternItem = null;
            lastLoggedBucket = -1;
            SetLanternVisualActive(false);
            return;
        }

        EnsureRepeller();
        EnsureHeldLight();

        float maxCharge = Mathf.Max(1f, currentItem.lanternMaxCharge);
        if (!chargeByItem.TryGetValue(currentItem, out float charge))
            charge = maxCharge;

        float drainPerSecond = Mathf.Max(0f, currentItem.lanternDrainPerSecond);
        if (charge > 0f && drainPerSecond > 0f)
            charge = Mathf.Max(0f, charge - drainPerSecond * Time.deltaTime);

        chargeByItem[currentItem] = charge;

        bool isLit = charge > 0f;
        SetLanternVisualActive(isLit);
        if (!isLit)
            return;

        lanternRepeller.clearRadius = Mathf.Max(0.1f, currentItem.lanternLightRadius);
        lanternRepeller.drawPoint = lanternDrawPoint != null ? lanternDrawPoint : transform;
        lanternRepeller.createsSafeZone = false;
        ConfigureHeldLight(currentItem);

        TryLogCharge(currentItem, charge, maxCharge);
    }

    void EnsureRepeller()
    {
        if (lanternDrawPoint == null)
            lanternDrawPoint = transform;

        if (lanternRepeller == null)
            lanternRepeller = GetComponent<FogRepeller>();

        if (lanternRepeller == null)
            lanternRepeller = gameObject.AddComponent<FogRepeller>();

        lanternRepeller.showGizmos = false;
        lanternRepeller.createsSafeZone = false;
        lanternRepeller.drawPoint = lanternDrawPoint;
    }

    void SetLanternVisualActive(bool active)
    {
        if (lanternRepeller != null && lanternRepeller.enabled != active)
            lanternRepeller.enabled = active;

        if (heldLanternLight != null && heldLanternLight.enabled != active)
            heldLanternLight.enabled = active;
    }

    void EnsureHeldLight()
    {
        if (heldLanternLight != null || !autoCreateHeldLight)
            return;

        Transform parent = lanternDrawPoint != null ? lanternDrawPoint : transform;
        Transform existing = parent.Find("HeldLanternLight2D");
        if (existing != null)
            heldLanternLight = existing.GetComponent<Light2D>();

        if (heldLanternLight == null)
        {
            GameObject lightObject = new GameObject("HeldLanternLight2D");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = Vector3.zero;
            heldLanternLight = lightObject.AddComponent<Light2D>();
        }

        ConfigureHeldLight(null);
        heldLanternLight.enabled = false;
    }

    void ConfigureHeldLight(ItemData item)
    {
        if (heldLanternLight == null)
            return;

        heldLanternLight.lightType = Light2D.LightType.Point;
        heldLanternLight.color = heldLightColor;
        heldLanternLight.intensity = heldLightIntensity;
        heldLanternLight.pointLightInnerRadius = heldLightInnerRadius;
        heldLanternLight.pointLightOuterRadius = heldLightVisualOuterRadius;
        heldLanternLight.falloffIntensity = heldLightFalloffIntensity;
        heldLanternLight.volumeIntensity = heldLightVolumeIntensity;
        heldLanternLight.volumetricEnabled = heldLightVolumetricEnabled && heldLightVolumeIntensity > 0f;
        heldLanternLight.shadowIntensity = heldLightShadowIntensity;
    }

    void TryLogCharge(ItemData item, float charge, float maxCharge)
    {
        if (!logCharge)
            return;

        if (item != lastLanternItem)
        {
            lastLanternItem = item;
            lastLoggedBucket = -1;
        }

        float step = Mathf.Max(1f, logStep);
        int bucket = Mathf.FloorToInt(charge / step);
        if (bucket == lastLoggedBucket)
            return;

        lastLoggedBucket = bucket;
        Debug.Log($"Lantern charge: {charge:0.0}/{maxCharge:0.0}");
    }

    bool TryGetLanternChargeInternal(ItemData item, out float currentCharge, out float maxCharge)
    {
        currentCharge = 0f;
        maxCharge = 0f;

        if (item == null)
            return false;

        bool isLanternItem = item.type == ItemType.Lantern || item.isLantern;
        if (!isLanternItem)
            return false;

        maxCharge = Mathf.Max(1f, item.lanternMaxCharge);
        if (!chargeByItem.TryGetValue(item, out currentCharge))
            currentCharge = maxCharge;

        return true;
    }
}

[DisallowMultipleComponent]
public class PlacedLantern : MonoBehaviour
{
    [SerializeField] private FogRepeller fogRepeller;
    [SerializeField] private ResourceDrop resourceDrop;
    [SerializeField] private ItemData sourceItem;
    [SerializeField] private float currentCharge;
    [SerializeField] private float maxCharge = 100f;
    [SerializeField] private float drainPerSecond = 1f;
    [SerializeField] private float pickupColliderRadius = 0.35f;

    void Awake()
    {
        EnsureComponents();
        ApplyLightState();
    }

    void Update()
    {
        if (currentCharge > 0f && drainPerSecond > 0f)
            currentCharge = Mathf.Max(0f, currentCharge - drainPerSecond * Time.deltaTime);

        ApplyLightState();
    }

    public void Initialize(ItemData lanternItem, float initialCharge, float configuredMaxCharge, float configuredDrainPerSecond)
    {
        sourceItem = lanternItem;
        maxCharge = Mathf.Max(1f, configuredMaxCharge);
        currentCharge = Mathf.Clamp(initialCharge, 0f, maxCharge);
        drainPerSecond = Mathf.Max(0f, configuredDrainPerSecond);

        EnsureComponents();
        ApplyLightState();
    }

    public bool TryPickup()
    {
        if (resourceDrop == null)
            resourceDrop = GetComponent<ResourceDrop>();

        if (resourceDrop == null)
            return false;

        resourceDrop.DropNow();

        Destroy(gameObject);
        return true;
    }

    public SavePlacedLanternData CreateSnapshot()
    {
        return new SavePlacedLanternData
        {
            itemId = SaveManager.GetItemId(sourceItem),
            position = SaveManager.ToSaveVector3(transform.position),
            rotationZ = transform.eulerAngles.z,
            currentCharge = currentCharge,
            maxCharge = maxCharge,
            drainPerSecond = drainPerSecond
        };
    }

    public void RestoreSnapshot(SavePlacedLanternData data, ItemData lanternItem)
    {
        if (data == null)
            return;

        Initialize(lanternItem, data.currentCharge, data.maxCharge, data.drainPerSecond);
    }

    void EnsureComponents()
    {
        if (fogRepeller == null)
            fogRepeller = GetComponent<FogRepeller>();

        if (fogRepeller == null)
            fogRepeller = gameObject.AddComponent<FogRepeller>();

        fogRepeller.createsSafeZone = false;
        fogRepeller.showGizmos = false;
        fogRepeller.drawPoint = transform;

        if (sourceItem != null)
            fogRepeller.clearRadius = Mathf.Max(0.1f, sourceItem.lanternLightRadius);

        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = Mathf.Max(0.05f, pickupColliderRadius);
            circle.isTrigger = true;
        }
    }

    void ApplyLightState()
    {
        if (fogRepeller == null)
            return;

        bool isLit = currentCharge > 0f;
        if (fogRepeller.enabled != isLit)
            fogRepeller.enabled = isLit;
    }
}
