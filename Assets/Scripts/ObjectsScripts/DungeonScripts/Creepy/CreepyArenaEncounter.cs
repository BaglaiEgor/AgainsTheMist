using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CreepyArenaEncounter : MonoBehaviour
{
    [Header("Doors")]
    [SerializeField] private List<CreepyDoorBlocker> doorsToClose = new();
    [SerializeField] private List<CreepyDoorBlocker> doorsToOpenOnComplete = new();

    [Header("Enemies")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private Transform[] enemySpawnPoints;
    [Min(0)] [SerializeField] private int totalEnemiesToSpawn = 6;
    [Min(1)] [SerializeField] private int maxAliveEnemies = 3;
    [Min(0.05f)] [SerializeField] private float spawnInterval = 1.2f;

    [Header("Targets")]
    [SerializeField] private List<CreepyDestructibleTarget> requiredTargets = new();

    [Header("Behaviour")]
    [SerializeField] private bool startOnce = true;
    [Min(0.1f)] [SerializeField] private float completionCheckInterval = 0.25f;
    [Min(0f)] [SerializeField] private float spawnDelay;

    private readonly List<GameObject> spawnedEnemies = new();
    private bool started;
    private bool completed;
    private int spawnedEnemyCount;
    private int nextEnemyPrefabIndex;
    private int nextSpawnPointIndex;

    public bool Completed => completed;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed || started && startOnce)
            return;

        if (other.GetComponentInParent<PlayerHealth>() == null)
            return;

        StartEncounter();
    }

    public void StartEncounter()
    {
        if (completed || started && startOnce)
            return;

        started = true;
        CloseDoors();
        StartCoroutine(EncounterRoutine());
    }

    private void CloseDoors()
    {
        for (int i = 0; i < doorsToClose.Count; i++)
        {
            if (doorsToClose[i] != null)
                doorsToClose[i].Close();
        }
    }

    private void OpenCompletionDoors()
    {
        for (int i = 0; i < doorsToOpenOnComplete.Count; i++)
        {
            if (doorsToOpenOnComplete[i] != null)
                doorsToOpenOnComplete[i].Open();
        }
    }

    private IEnumerator EncounterRoutine()
    {
        if (spawnDelay > 0f)
            yield return new WaitForSeconds(spawnDelay);

        ActivateTargets();

        WaitForSeconds wait = new WaitForSeconds(completionCheckInterval);
        float nextSpawnTime = Time.time;
        while (!IsEnemySpawningComplete() || !AreEnemiesDefeated() || !AreTargetsDestroyed())
        {
            RemoveDeadEnemies();

            if (CanSpawnEnemy() && Time.time >= nextSpawnTime)
            {
                SpawnNextEnemy();
                nextSpawnTime = Time.time + spawnInterval;
            }

            yield return wait;
        }

        CompleteEncounter();
    }

    private void ActivateTargets()
    {
        for (int i = 0; i < requiredTargets.Count; i++)
        {
            if (requiredTargets[i] != null)
                requiredTargets[i].ActivateTarget();
        }
    }

    private bool CanSpawnEnemy()
    {
        if (enemyPrefabs == null || enemySpawnPoints == null)
            return false;

        if (enemyPrefabs.Length == 0 || enemySpawnPoints.Length == 0)
            return false;

        if (spawnedEnemyCount >= totalEnemiesToSpawn)
            return false;

        return spawnedEnemies.Count < maxAliveEnemies;
    }

    private void SpawnNextEnemy()
    {
        GameObject prefab = GetNextEnemyPrefab();
        Transform spawnPoint = GetNextSpawnPoint();
        if (prefab == null || spawnPoint == null)
            return;

        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        spawnedEnemies.Add(enemy);
        spawnedEnemyCount++;
    }

    private GameObject GetNextEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            int index = nextEnemyPrefabIndex % enemyPrefabs.Length;
            nextEnemyPrefabIndex++;

            if (enemyPrefabs[index] != null)
                return enemyPrefabs[index];
        }

        return null;
    }

    private Transform GetNextSpawnPoint()
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            return null;

        for (int i = 0; i < enemySpawnPoints.Length; i++)
        {
            int index = nextSpawnPointIndex % enemySpawnPoints.Length;
            nextSpawnPointIndex++;

            if (enemySpawnPoints[index] != null)
                return enemySpawnPoints[index];
        }

        return null;
    }

    private bool IsEnemySpawningComplete()
    {
        return enemyPrefabs == null ||
               enemyPrefabs.Length == 0 ||
               enemySpawnPoints == null ||
               enemySpawnPoints.Length == 0 ||
               spawnedEnemyCount >= totalEnemiesToSpawn;
    }

    private bool AreEnemiesDefeated()
    {
        RemoveDeadEnemies();
        return spawnedEnemies.Count == 0;
    }

    private void RemoveDeadEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemyObject = spawnedEnemies[i];
            if (enemyObject == null)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }

            FogEnemy fogEnemy = enemyObject.GetComponent<FogEnemy>();
            if (fogEnemy != null && fogEnemy.IsDead)
            {
                spawnedEnemies.RemoveAt(i);
                continue;
            }
        }
    }

    private bool AreTargetsDestroyed()
    {
        for (int i = 0; i < requiredTargets.Count; i++)
        {
            CreepyDestructibleTarget target = requiredTargets[i];
            if (target != null && !target.IsDestroyed)
                return false;
        }

        return true;
    }

    private void CompleteEncounter()
    {
        if (completed)
            return;

        completed = true;
        OpenCompletionDoors();
    }
}
