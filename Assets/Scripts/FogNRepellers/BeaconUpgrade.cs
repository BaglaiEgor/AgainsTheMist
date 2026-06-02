using System;
using UnityEngine;
using System.Collections.Generic;

public readonly struct BeaconUpgradeSnapshot
{
    public readonly int level;
    public readonly float radius;

    public BeaconUpgradeSnapshot(int level, float radius)
    {
        this.level = Mathf.Max(0, level);
        this.radius = Mathf.Max(0f, radius);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(FogRepeller))]
public class BeaconUpgrade : MonoBehaviour
{
    [System.Serializable]
    public class RequiredResource
    {
        public ItemData item;
        [Min(1)]
        public int amount = 1;
    }

    [System.Serializable]
    public class UpgradeLevel
    {
        [Min(0.01f)]
        public float radiusIncrease = 1f;
        public RequiredResource[] requiredResources;
    }

    [Header("Links")]
    [SerializeField] private FogRepeller fogRepeller;

    [Header("Interaction")]
    [Min(0.2f)] [SerializeField] private float interactRadius = 1.6f;

    [Header("Upgrade Levels")]
    [SerializeField] private List<UpgradeLevel> upgradeLevels = new List<UpgradeLevel>();

    // Legacy fields are kept only to migrate existing scene/prefab data once.
    [SerializeField, HideInInspector] private ItemData upgradeResource;
    [SerializeField, HideInInspector] private int resourceCost = 1;
    [SerializeField, HideInInspector] private float radiusIncrease = 1f;
    [SerializeField, HideInInspector] private ItemData secondUpgradeFogResource;
    [SerializeField, HideInInspector] private int secondUpgradeFogCost = 2;
    [SerializeField, HideInInspector] private ItemData secondUpgradeFarmResource;
    [SerializeField, HideInInspector] private int secondUpgradeFarmCost = 2;
    [SerializeField, HideInInspector] private float secondUpgradeRadiusIncrease = 1.5f;

    [Header("Runtime")]
    [SerializeField] private int currentUpgradeLevel;

    public event Action OnBeaconUpgradeStateChanged;

    void Reset()
    {
        EnsureRepeller();
    }

    void Awake()
    {
        EnsureRepeller();
        EnsureUpgradeLevelsInitialized();
    }

    public bool TryUpgrade(Inventory inventory)
    {
        return InventoryTransactionService.TryBeaconUpgrade(this, inventory);
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return false;

        float maxDistance = Mathf.Max(0.2f, interactRadius);
        return Vector2.Distance(transform.position, interactor.position) <= maxDistance;
    }

    public int CurrentLevel => Mathf.Max(0, currentUpgradeLevel);
    public int MaxLevel => upgradeLevels != null ? upgradeLevels.Count : 0;
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;
    public float CurrentRadius => fogRepeller != null ? Mathf.Max(0f, fogRepeller.clearRadius) : 0f;

    public bool TryGetNextLevel(out UpgradeLevel nextLevel)
    {
        EnsureUpgradeLevelsInitialized();
        nextLevel = null;

        if (upgradeLevels == null || currentUpgradeLevel < 0 || currentUpgradeLevel >= upgradeLevels.Count)
            return false;

        nextLevel = upgradeLevels[currentUpgradeLevel];
        return nextLevel != null;
    }

    public BeaconUpgradeSnapshot CreateSnapshot()
    {
        EnsureRepeller();
        return new BeaconUpgradeSnapshot(currentUpgradeLevel, fogRepeller != null ? fogRepeller.clearRadius : 0f);
    }

    public void RestoreSnapshot(BeaconUpgradeSnapshot snapshot)
    {
        EnsureRepeller();
        currentUpgradeLevel = Mathf.Max(0, snapshot.level);
        if (fogRepeller != null)
        {
            fogRepeller.createsSafeZone = true;
            fogRepeller.clearRadius = Mathf.Max(0f, snapshot.radius);
        }

        RefreshFogVisuals();
        OnBeaconUpgradeStateChanged?.Invoke();
    }

    public bool ApplyUpgradeFromTransaction(UpgradeLevel nextLevel)
    {
        EnsureRepeller();
        EnsureUpgradeLevelsInitialized();

        if (fogRepeller == null || nextLevel == null)
            return false;
        if (upgradeLevels == null || currentUpgradeLevel < 0 || currentUpgradeLevel >= upgradeLevels.Count)
            return false;
        if (!ReferenceEquals(upgradeLevels[currentUpgradeLevel], nextLevel))
            return false;

        fogRepeller.createsSafeZone = true;
        fogRepeller.clearRadius += Mathf.Max(0.01f, nextLevel.radiusIncrease);
        currentUpgradeLevel++;
        RefreshFogVisuals();
        OnBeaconUpgradeStateChanged?.Invoke();

        Debug.Log($"Upgrade success. Level {currentUpgradeLevel}/{upgradeLevels.Count}");
        return true;
    }

    void EnsureRepeller()
    {
        if (fogRepeller != null)
        {
            fogRepeller.createsSafeZone = true;
            return;
        }

        fogRepeller = GetComponent<FogRepeller>();
        if (fogRepeller == null)
            fogRepeller = GetComponentInChildren<FogRepeller>();

        if (fogRepeller != null)
            fogRepeller.createsSafeZone = true;
    }

    void RefreshFogVisuals()
    {
        FogMaskDrawer drawer = FindFirstObjectByType<FogMaskDrawer>();
        if (drawer != null)
            drawer.ForceUpdate();
    }

    void EnsureUpgradeLevelsInitialized()
    {
        if (upgradeLevels == null)
            upgradeLevels = new List<UpgradeLevel>();

        if (upgradeLevels.Count > 0)
            return;

        TryMigrateLegacyLevels();
    }

    void TryMigrateLegacyLevels()
    {
        bool hasFirstLegacyLevel = upgradeResource != null;
        bool hasSecondLegacyLevel = secondUpgradeFogResource != null || secondUpgradeFarmResource != null;

        if (!hasFirstLegacyLevel && !hasSecondLegacyLevel)
            return;

        upgradeLevels.Clear();

        if (hasFirstLegacyLevel)
        {
            UpgradeLevel firstLevel = new UpgradeLevel
            {
                radiusIncrease = Mathf.Max(0.01f, radiusIncrease),
                requiredResources = new[]
                {
                    new RequiredResource
                    {
                        item = upgradeResource,
                        amount = Mathf.Max(1, resourceCost)
                    }
                }
            };
            upgradeLevels.Add(firstLevel);
        }

        if (hasSecondLegacyLevel)
        {
            List<RequiredResource> secondLevelRequirements = new List<RequiredResource>();

            if (secondUpgradeFogResource != null)
            {
                secondLevelRequirements.Add(new RequiredResource
                {
                    item = secondUpgradeFogResource,
                    amount = Mathf.Max(1, secondUpgradeFogCost)
                });
            }

            if (secondUpgradeFarmResource != null)
            {
                secondLevelRequirements.Add(new RequiredResource
                {
                    item = secondUpgradeFarmResource,
                    amount = Mathf.Max(1, secondUpgradeFarmCost)
                });
            }

            UpgradeLevel secondLevel = new UpgradeLevel
            {
                radiusIncrease = Mathf.Max(0.01f, secondUpgradeRadiusIncrease),
                requiredResources = secondLevelRequirements.ToArray()
            };
            upgradeLevels.Add(secondLevel);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        interactRadius = Mathf.Max(0.2f, interactRadius);

        if (upgradeLevels == null)
            upgradeLevels = new List<UpgradeLevel>();

        for (int levelIndex = 0; levelIndex < upgradeLevels.Count; levelIndex++)
        {
            UpgradeLevel level = upgradeLevels[levelIndex];
            if (level == null)
                continue;

            level.radiusIncrease = Mathf.Max(0.01f, level.radiusIncrease);
            if (level.requiredResources == null)
                continue;

            for (int reqIndex = 0; reqIndex < level.requiredResources.Length; reqIndex++)
            {
                RequiredResource requirement = level.requiredResources[reqIndex];
                if (requirement == null)
                    continue;

                requirement.amount = Mathf.Max(1, requirement.amount);
            }
        }

        currentUpgradeLevel = Mathf.Max(0, currentUpgradeLevel);
        if (upgradeLevels.Count > 0)
            currentUpgradeLevel = Mathf.Min(currentUpgradeLevel, upgradeLevels.Count);
    }
#endif
}
