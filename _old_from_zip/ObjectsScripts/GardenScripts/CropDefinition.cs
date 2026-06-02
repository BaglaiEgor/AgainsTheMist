using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CropDefinition", menuName = "Farm/Garden/Crop")]
public class CropDefinition : ScriptableObject
{
    [Serializable]
    public class GrowthVisualStage
    {
        [Range(0f, 1f)]
        public float growth01;
        public Sprite sprite;
    }

    [Header("Id")]
    [SerializeField] private string cropId = "crop";
    [SerializeField] private string displayName = "New Crop";

    [Header("Growth")]
    [Min(1)] [SerializeField] private int growthMinutes = 720;
    [Range(0f, 1f)] [SerializeField] private float growthMultiplierInFog = 1f;
    [Range(0f, 1f)] [SerializeField] private float growthMultiplierOutsideFog = 1f;

    [Header("Visual")]
    [SerializeField] private List<GrowthVisualStage> growthStages = new();

    [Header("Harvest")]
    [SerializeField] private Drop[] harvestDrops;

    public string CropId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(cropId))
                return name;

            return cropId;
        }
    }

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return name;

            return displayName;
        }
    }

    public int GrowthMinutes => growthMinutes;
    public float GrowthMultiplierInFog => Mathf.Clamp01(growthMultiplierInFog);
    public float GrowthMultiplierOutsideFog => Mathf.Clamp01(growthMultiplierOutsideFog);
    public Drop[] HarvestDrops => harvestDrops;

    public Sprite GetSpriteForState(GardenBedState state, float growthNormalized)
    {
        return GetGrowthSprite(growthNormalized);
    }

    public Sprite GetGrowthSprite(float growthNormalized)
    {
        if (growthStages == null || growthStages.Count == 0)
            return null;

        float progress = Mathf.Clamp01(growthNormalized);
        Sprite selected = null;
        float selectedThreshold = float.MinValue;

        for (int i = 0; i < growthStages.Count; i++)
        {
            GrowthVisualStage stage = growthStages[i];
            if (stage == null || stage.sprite == null)
                continue;

            float stageThreshold = Mathf.Clamp01(stage.growth01);
            if (stageThreshold > progress || stageThreshold < selectedThreshold)
                continue;

            selectedThreshold = stageThreshold;
            selected = stage.sprite;
        }

        if (selected != null)
            return selected;

        for (int i = 0; i < growthStages.Count; i++)
        {
            GrowthVisualStage stage = growthStages[i];
            if (stage != null && stage.sprite != null)
                return stage.sprite;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        growthMinutes = Mathf.Max(1, growthMinutes);
        growthMultiplierInFog = Mathf.Clamp01(growthMultiplierInFog);
        growthMultiplierOutsideFog = Mathf.Clamp01(growthMultiplierOutsideFog);
    }
#endif
}
