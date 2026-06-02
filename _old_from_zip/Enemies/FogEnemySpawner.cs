using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FogEnemySpawner : MonoBehaviour
{
    private const string DefaultFrostEnemyResourcePath = "Enemies/FrostFogEnemy";

    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject secondaryEnemyPrefab;
    [SerializeField] private GameObject frostZoneEnemyPrefab;
    [SerializeField] private PlayerFogPressure playerFogPressure;
    [Range(0f, 1f)]
    [SerializeField] private float secondaryEnemyChance = 0.35f;
    [SerializeField] private FogEnemyBehaviorType secondaryEnemyBehavior = FogEnemyBehaviorType.HitAndRun;

    [Header("Spawn")]
    [SerializeField] private float spawnCheckInterval = 1f;
    [SerializeField] private float minSpawnCheckInterval = 0.2f;
    [SerializeField] private int maxAliveEnemies = 1;
    [SerializeField] private float minSpawnDistance = 3f;
    [SerializeField] private float maxSpawnDistance = 7f;
    [SerializeField] private int spawnPositionTries = 10;

    private readonly List<FogEnemy> activeEnemies = new List<FogEnemy>();
    private float spawnCheckTimer;

    void Awake()
    {
        EnsureFrostEnemyPrefab();
    }

    void Update()
    {
        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        if (!TryResolvePlayer(out Transform playerTransform))
            return;

        TryResolvePlayerFogPressure(playerTransform);
        EnsureFrostEnemyPrefab();

        if (enemyPrefab == null && secondaryEnemyPrefab == null && frostZoneEnemyPrefab == null)
            return;

        CleanupDeadEnemies();
        if (activeEnemies.Count >= Mathf.Max(0, maxAliveEnemies))
            return;

        float interval = GetCurrentSpawnCheckInterval();
        spawnCheckTimer += Time.deltaTime;
        if (spawnCheckTimer < interval)
            return;

        spawnCheckTimer = 0f;

        if (!fogSystem.IsPositionInFog(playerTransform.position))
            return;

        bool playerInFrostZone = FrostFogZone.IsAnyZoneActiveAtPosition(playerTransform.position);
        bool shouldSpawnFrostEnemy = playerInFrostZone && frostZoneEnemyPrefab != null;

        if (!TryGetSpawnPosition(fogSystem, playerTransform.position, shouldSpawnFrostEnemy, out Vector3 spawnPosition))
            return;

        bool spawnedSecondaryEnemy;
        GameObject prefabToSpawn = ChooseEnemyPrefab(shouldSpawnFrostEnemy, out spawnedSecondaryEnemy);
        if (prefabToSpawn == null)
            return;

        GameObject spawned = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        FogEnemy enemy = spawned.GetComponent<FogEnemy>();
        if (enemy != null)
        {
            if (spawnedSecondaryEnemy)
                enemy.ConfigureBehavior(secondaryEnemyBehavior);

            activeEnemies.Add(enemy);
        }
    }

    GameObject ChooseEnemyPrefab(bool preferFrostZoneEnemy, out bool isSecondary)
    {
        isSecondary = false;

        if (preferFrostZoneEnemy && frostZoneEnemyPrefab != null)
            return frostZoneEnemyPrefab;

        if (enemyPrefab == null && secondaryEnemyPrefab == null)
            return null;

        if (enemyPrefab == null)
        {
            isSecondary = true;
            return secondaryEnemyPrefab;
        }

        if (secondaryEnemyPrefab == null)
            return enemyPrefab;

        bool spawnSecondary = Random.value <= Mathf.Clamp01(secondaryEnemyChance);
        isSecondary = spawnSecondary;
        return spawnSecondary ? secondaryEnemyPrefab : enemyPrefab;
    }

    float GetCurrentSpawnCheckInterval()
    {
        float baseInterval = Mathf.Max(0.01f, spawnCheckInterval);
        float minInterval = Mathf.Clamp(minSpawnCheckInterval, 0.01f, baseInterval);

        if (playerFogPressure == null)
            return baseInterval;

        return Mathf.Lerp(baseInterval, minInterval, playerFogPressure.NormalizedFogPressure);
    }

    bool TryResolvePlayer(out Transform playerTransform)
    {
        if (player != null)
        {
            playerTransform = player;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        playerTransform = player;
        return playerTransform != null;
    }

    void TryResolvePlayerFogPressure(Transform playerTransform)
    {
        if (playerFogPressure != null || playerTransform == null)
            return;

        playerFogPressure = playerTransform.GetComponent<PlayerFogPressure>();
    }

    void CleanupDeadEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null)
                activeEnemies.RemoveAt(i);
        }
    }

    bool TryGetSpawnPosition(FogSystem fogSystem, Vector3 playerPosition, bool requireFrostZone, out Vector3 spawnPosition)
    {
        float minDistance = Mathf.Max(0f, minSpawnDistance);
        float maxDistance = Mathf.Max(minDistance, maxSpawnDistance);
        int tries = Mathf.Max(1, spawnPositionTries);

        for (int i = 0; i < tries; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector2.right;

            float distance = Random.Range(minDistance, maxDistance);
            Vector3 candidate = playerPosition + new Vector3(direction.x, direction.y, 0f) * distance;
            candidate.z = playerPosition.z;

            if (!fogSystem.IsPositionInFog(candidate))
                continue;
            if (requireFrostZone && !FrostFogZone.IsAnyZoneActiveAtPosition(candidate))
                continue;

            spawnPosition = candidate;
            return true;
        }

        spawnPosition = default;
        return false;
    }

    void EnsureFrostEnemyPrefab()
    {
        if (frostZoneEnemyPrefab != null)
            return;

        frostZoneEnemyPrefab = Resources.Load<GameObject>(DefaultFrostEnemyResourcePath);
    }
}
