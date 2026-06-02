using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFogPressure : MonoBehaviour
{
    [Header("Pressure")]
    [Range(0f, 100f)]
    [SerializeField] private float fogPressure;
    [SerializeField] private float pressureIncreasePerSecond = 10f;
    [SerializeField] private float pressureDecreasePerSecond = 14f;
    [Header("Depth Scaling")]
    [SerializeField] private float depthForMaxPressureMultiplier = 12f;
    [SerializeField] private float maxPressureMultiplierByDepth = 2f;
    [Range(0.01f, 1f)]
    [SerializeField] private float lanternFogPressureMultiplier = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool logPressure = true;
    [SerializeField] private float logStep = 10f;

    private int lastLoggedBucket = -1;
    private Inventory playerInventory;
    private PlayerPotionEffects playerPotionEffects;

    public float FogPressure => fogPressure;
    public float NormalizedFogPressure => fogPressure / 100f;

    void Update()
    {
        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        EnsureInventoryReference();
        EnsurePotionEffectsReference();

        float fogDepth = fogSystem.GetFogDepth(transform.position);
        bool inFog = fogDepth > 0f;
        float delta;
        if (!inFog)
        {
            delta = -pressureDecreasePerSecond;
        }
        else
        {
            float normalizedDepth = Mathf.Clamp01(
                fogDepth / Mathf.Max(0.01f, depthForMaxPressureMultiplier)
            );
            float depthMultiplier = Mathf.Lerp(
                1f,
                Mathf.Max(1f, maxPressureMultiplierByDepth),
                normalizedDepth
            );

            bool inLanternLight = fogSystem.IsPositionInLanternLight(transform.position);
            float multiplier = inLanternLight ? Mathf.Max(0.01f, lanternFogPressureMultiplier) : 1f;
            float equipmentMultiplier = ResolvePressureGrowthMultiplier();
            float potionMultiplier = ResolvePotionPressureGrowthMultiplier();
            delta = pressureIncreasePerSecond * depthMultiplier * multiplier * equipmentMultiplier * potionMultiplier;
        }

        float previousPressure = fogPressure;

        fogPressure = Mathf.Clamp(fogPressure + delta * Time.deltaTime, 0f, 100f);

        if (!logPressure || Mathf.Approximately(previousPressure, fogPressure))
            return;

        float step = Mathf.Max(0.1f, logStep);
        int currentBucket = Mathf.FloorToInt(fogPressure / step);
        if (currentBucket == lastLoggedBucket)
            return;

        lastLoggedBucket = currentBucket;
        Debug.Log($"Fog pressure: {fogPressure:0.0}");
    }

    void EnsureInventoryReference()
    {
        if (playerInventory != null)
            return;

        playerInventory = GetComponent<Inventory>();
        if (playerInventory == null)
            playerInventory = GetComponentInParent<Inventory>();
        if (playerInventory == null)
            playerInventory = GetComponentInChildren<Inventory>();

        if (playerInventory != null)
            return;

        Inventory[] allInventories = Object.FindObjectsByType<Inventory>(FindObjectsSortMode.None);
        float bestDistance = float.MaxValue;
        for (int i = 0; i < allInventories.Length; i++)
        {
            Inventory candidate = allInventories[i];
            if (candidate == null)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            playerInventory = candidate;
        }
    }

    float ResolvePressureGrowthMultiplier()
    {
        if (playerInventory == null)
            return 1f;

        ItemData equippedItem = playerInventory.EquippedItem;
        if (equippedItem == null || equippedItem.type != ItemType.Equipment)
            return 1f;

        // Pressure should still grow in fog, even with protection equipment.
        return Mathf.Clamp(equippedItem.pressureGrowthMultiplier, 0.05f, 10f);
    }

    float ResolvePotionPressureGrowthMultiplier()
    {
        if (playerPotionEffects == null)
            return 1f;

        return Mathf.Max(0f, playerPotionEffects.GetPressureGrowthMultiplier());
    }

    void EnsurePotionEffectsReference()
    {
        if (playerPotionEffects != null)
            return;

        playerPotionEffects = GetComponent<PlayerPotionEffects>();
        if (playerPotionEffects == null)
            playerPotionEffects = GetComponentInParent<PlayerPotionEffects>();
        if (playerPotionEffects == null)
            playerPotionEffects = GetComponentInChildren<PlayerPotionEffects>();
    }
}
