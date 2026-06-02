using UnityEngine;

public class GardenBed : MonoBehaviour
{
    private const int HoursPerDay = 24;
    private const int MinutesPerDay = HoursPerDay * MinutesPerHour;
    private const int MinutesPerHour = 60;

    [Header("References")]
    [SerializeField] private GameTimeSystem timeSystem;
    [SerializeField] private SpriteRenderer plantRenderer;
    [SerializeField] private Transform dropSpawnPoint;

    [Header("Runtime State")]
    [SerializeField] private GardenBedState state = GardenBedState.Empty;
    [SerializeField] private CropDefinition plantedCrop;
    [Min(0)] [SerializeField] private int growthMinutes;
    [Header("Fog Growth (Fallback for old crops)")]
    [Range(0f, 1f)] [SerializeField] private float growthMultiplierInFog = 0f;

    private int lastAbsoluteMinute;
    private bool hasTimeBaseline;

    public GardenBedState State => state;
    public CropDefinition PlantedCrop => plantedCrop;
    public bool IsEmpty => state == GardenBedState.Empty;
    public float Growth01
    {
        get
        {
            if (plantedCrop == null)
                return 0f;

            int requiredGrowth = Mathf.Max(1, plantedCrop.GrowthMinutes);
            return Mathf.Clamp01(growthMinutes / (float)requiredGrowth);
        }
    }

    private void OnEnable()
    {
        TryBindTimeSystem();
        SubscribeToTime();
        RefreshVisual();
    }

    private void OnDisable()
    {
        UnsubscribeFromTime();
    }

    public bool TryPlant(CropDefinition crop)
    {
        if (crop == null || state != GardenBedState.Empty)
            return false;

        plantedCrop = crop;
        state = GardenBedState.Growing;
        growthMinutes = 0;

        RefreshVisual();
        return true;
    }

    public bool TryHarvest()
    {
        if (state != GardenBedState.ReadyToHarvest || plantedCrop == null)
            return false;

        SpawnDrops(plantedCrop.HarvestDrops);
        ClearBed();
        return true;
    }

    public void AdvanceMinutes(int minutes)
    {
        if (minutes <= 0)
            return;

        Tick(minutes);
    }

    private void Tick(int minutes)
    {
        if (minutes <= 0)
            return;

        if (plantedCrop == null || state != GardenBedState.Growing)
            return;

        int growthDelta = GetGrowthDelta(minutes);
        if (growthDelta <= 0)
            return;

        growthMinutes += growthDelta;
        int requiredGrowth = Mathf.Max(1, plantedCrop.GrowthMinutes);
        if (growthMinutes >= requiredGrowth)
        {
            growthMinutes = requiredGrowth;
            state = GardenBedState.ReadyToHarvest;
        }

        RefreshVisual();
    }

    private int GetGrowthDelta(int baseMinutes)
    {
        if (baseMinutes <= 0)
            return 0;

        FogSystem fogSystem = FogSystem.Instance;
        bool inFog = fogSystem != null && fogSystem.IsPositionInFog(transform.position);
        float multiplier;
        if (plantedCrop != null)
        {
            multiplier = inFog
                ? plantedCrop.GrowthMultiplierInFog
                : plantedCrop.GrowthMultiplierOutsideFog;
        }
        else
        {
            multiplier = inFog ? Mathf.Clamp01(growthMultiplierInFog) : 1f;
        }

        if (multiplier <= 0f)
            return 0;

        return Mathf.FloorToInt(baseMinutes * multiplier);
    }

    private void OnMinuteChanged(int day, int hour, int minute)
    {
        int absoluteMinute = (Mathf.Max(1, day) - 1) * MinutesPerDay;
        absoluteMinute += Mathf.Clamp(hour, 0, 23) * MinutesPerHour;
        absoluteMinute += Mathf.Clamp(minute, 0, 59);

        if (!hasTimeBaseline)
        {
            hasTimeBaseline = true;
            lastAbsoluteMinute = absoluteMinute;
            return;
        }

        int delta = absoluteMinute - lastAbsoluteMinute;
        lastAbsoluteMinute = absoluteMinute;

        if (delta > 0)
            Tick(delta);
    }

    private void SpawnDrops(Drop[] drops)
    {
        if (drops == null || drops.Length == 0)
            return;

        Vector3 spawnCenter = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;

        for (int i = 0; i < drops.Length; i++)
        {
            Drop drop = drops[i];
            if (drop == null || drop.prefab == null)
                continue;

            if (Random.value > Mathf.Clamp01(drop.chance))
                continue;

            int minAmount = Mathf.Max(0, drop.minAmount);
            int maxAmount = Mathf.Max(minAmount, drop.maxAmount);
            int count = Random.Range(minAmount, maxAmount + 1);

            for (int amountIndex = 0; amountIndex < count; amountIndex++)
                Instantiate(drop.prefab, spawnCenter, Quaternion.identity);
        }
    }

    private void ClearBed()
    {
        state = GardenBedState.Empty;
        plantedCrop = null;
        growthMinutes = 0;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (plantRenderer == null)
            return;

        if (state == GardenBedState.Empty || plantedCrop == null)
        {
            plantRenderer.sprite = null;
            plantRenderer.enabled = false;
            return;
        }

        Sprite stageSprite = plantedCrop.GetSpriteForState(state, Growth01);
        plantRenderer.sprite = stageSprite;
        plantRenderer.enabled = stageSprite != null;
    }

    private void TryBindTimeSystem()
    {
        if (timeSystem != null)
            return;

        timeSystem = Object.FindFirstObjectByType<GameTimeSystem>();
    }

    private void SubscribeToTime()
    {
        hasTimeBaseline = false;

        if (timeSystem != null)
            timeSystem.OnMinuteChanged += OnMinuteChanged;
    }

    private void UnsubscribeFromTime()
    {
        if (timeSystem != null)
            timeSystem.OnMinuteChanged -= OnMinuteChanged;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        growthMinutes = Mathf.Max(0, growthMinutes);

        if (!Application.isPlaying)
            RefreshVisual();
    }
#endif
}
