using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldObjectSpawner : MonoBehaviour
{
    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap snowTilemap;
    [SerializeField] private Tilemap upperSnowTilemap;
    [SerializeField] private Tilemap waterTilemap;
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap buildTilemap;
    [SerializeField] private Tilemap decorTilemap;
    [SerializeField] private Tilemap pathTilemap;

    [Header("Spawn Tables")]
    [SerializeField] private List<SpawnableObject> spawnTable;

    [Header("Initial Spawn")]
    [SerializeField] private int initialObjects = 40;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnInterval = 60f;
    [SerializeField] private int respawnPerTick = 2;

    [Header("Parents")]
    [SerializeField] private Transform objectsParent;

    private readonly List<Vector3Int> footprintBuffer = new();
    private readonly List<int> spawnCandidateIndices = new();

    void Start()
    {
        EnsureTilemaps();
        WorldGrid.ConfigureTilemaps(groundTilemap, waterTilemap, obstacleTilemap, buildTilemap, snowTilemap, decor: decorTilemap, path: pathTilemap, upperSnow: upperSnowTilemap);
        InitialSpawn();
        StartCoroutine(RespawnRoutine());
    }

    void InitialSpawn()
    {
        List<Vector3Int> freeCells = GetFreeCells();
        Shuffle(freeCells);
        SpawnFromFreeCells(freeCells, initialObjects);
    }

    IEnumerator RespawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(respawnInterval);
            Respawn();
        }
    }

    void Respawn()
    {
        List<Vector3Int> freeCells = GetFreeCells();
        if (freeCells.Count == 0)
            return;

        Shuffle(freeCells);
        SpawnFromFreeCells(freeCells, respawnPerTick);
    }

    void SpawnFromFreeCells(List<Vector3Int> freeCells, int desiredCount)
    {
        if (freeCells == null || freeCells.Count == 0 || desiredCount <= 0)
            return;

        if (!TryGetGroundBounds(out BoundsInt bounds))
            return;

        if (!TryGetGroundExtents(bounds, out int minX, out int maxX, out int minY, out int maxY))
            return;

        int spawned = 0;

        for (int i = 0; i < freeCells.Count && spawned < desiredCount; i++)
        {
            Vector3Int cell = freeCells[i];
            if (TrySpawnAtCell(cell, minX, maxX, minY, maxY))
                spawned++;
        }
    }

    List<Vector3Int> GetFreeCells()
    {
        List<Vector3Int> result = new List<Vector3Int>();
        HashSet<Vector3Int> addedCells = new HashSet<Vector3Int>();

        if (!TryGetGroundBounds(out BoundsInt bounds))
            return result;

        if (!TryGetGroundExtents(bounds, out int minX, out int maxX, out int minY, out int maxY))
            return result;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!WorldGrid.HasGround(pos))
                continue;

            if (IsEdgeCell(pos, minX, maxX, minY, maxY))
                continue;

            if (!WorldGrid.CanSpawn(pos))
                continue;

            if (addedCells.Add(pos))
                result.Add(pos);
        }

        return result;
    }

    bool TryGetGroundBounds(out BoundsInt bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (groundTilemap != null)
            AddBounds(groundTilemap.cellBounds, ref bounds, ref hasBounds);

        if (snowTilemap != null)
            AddBounds(snowTilemap.cellBounds, ref bounds, ref hasBounds);

        if (upperSnowTilemap != null)
            AddBounds(upperSnowTilemap.cellBounds, ref bounds, ref hasBounds);

        return hasBounds;
    }

    void AddBounds(BoundsInt source, ref BoundsInt bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = source;
            hasBounds = true;
            return;
        }

        int minX = Mathf.Min(bounds.xMin, source.xMin);
        int minY = Mathf.Min(bounds.yMin, source.yMin);
        int minZ = Mathf.Min(bounds.zMin, source.zMin);
        int maxX = Mathf.Max(bounds.xMax, source.xMax);
        int maxY = Mathf.Max(bounds.yMax, source.yMax);
        int maxZ = Mathf.Max(bounds.zMax, source.zMax);

        bounds = new BoundsInt(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    bool TryGetGroundExtents(BoundsInt bounds, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = int.MaxValue;
        maxX = int.MinValue;
        minY = int.MaxValue;
        maxY = int.MinValue;
        bool foundGround = false;

        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            if (!WorldGrid.HasGround(pos))
                continue;

            foundGround = true;
            minX = Mathf.Min(minX, pos.x);
            maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
        }

        return foundGround;
    }

    bool IsEdgeCell(Vector3Int pos, int minX, int maxX, int minY, int maxY)
    {
        if (pos.x == minX || pos.x == maxX || pos.y == minY || pos.y == maxY)
            return true;

        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector3Int neighbor = pos + CardinalDirections[i];
            if (!WorldGrid.HasGround(neighbor) || WorldGrid.HasWater(neighbor))
                return true;
        }

        return false;
    }

    bool CanSpawnPrefabAtCell(GameObject prefab, Vector3Int anchorCell, int minX, int maxX, int minY, int maxY)
    {
        WorldObjectOccupier occupier = prefab.GetComponent<WorldObjectOccupier>();
        if (occupier == null)
            return WorldGrid.CanSpawn(anchorCell);

        WorldObjectOccupier.BuildFootprintCells(
            anchorCell,
            occupier.width,
            occupier.height,
            occupier.CenterOnTransform,
            footprintBuffer
        );

        if (footprintBuffer.Count == 0)
            return false;

        for (int i = 0; i < footprintBuffer.Count; i++)
        {
            if (IsEdgeCell(footprintBuffer[i], minX, maxX, minY, maxY))
                return false;
        }

        for (int i = 0; i < footprintBuffer.Count; i++)
        {
            if (!WorldGrid.CanSpawn(footprintBuffer[i]))
                return false;
        }

        return true;
    }

    bool TrySpawnAtCell(Vector3Int cell, int minX, int maxX, int minY, int maxY)
    {
        if (spawnTable == null || spawnTable.Count == 0)
            return false;

        spawnCandidateIndices.Clear();
        for (int i = 0; i < spawnTable.Count; i++)
        {
            if (spawnTable[i].weight > 0)
                spawnCandidateIndices.Add(i);
        }

        while (spawnCandidateIndices.Count > 0)
        {
            int pickListIndex = PickWeightedCandidateIndex(spawnCandidateIndices);
            int spawnTableIndex = spawnCandidateIndices[pickListIndex];
            spawnCandidateIndices.RemoveAt(pickListIndex);

            GameObject prefab = spawnTable[spawnTableIndex].prefab;
            if (prefab == null)
                continue;

            if (!CanSpawnPrefabAtCell(prefab, cell, minX, maxX, minY, maxY))
                continue;

            Vector3 worldPos = GetCellCenterWorld(cell);
            GameObject spawnedObject = Instantiate(prefab, worldPos, Quaternion.identity, objectsParent);
            FogObjectTint.EnsureOn(spawnedObject);
            MarkAsSaveable(spawnedObject, prefab.name);
            return true;
        }

        return false;
    }

    Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        Tilemap cellTilemap = ResolveCellTilemap(cell);
        return cellTilemap != null ? cellTilemap.GetCellCenterWorld(cell) : Vector3.zero;
    }

    void EnsureTilemaps()
    {
        if (groundTilemap != null && snowTilemap != null && upperSnowTilemap != null && waterTilemap != null && obstacleTilemap != null && buildTilemap != null && decorTilemap != null && pathTilemap != null)
            return;

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");
        int snowLayer = LayerMask.NameToLayer("Snow");
        int waterLayer = LayerMask.NameToLayer("Water");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int buildLayer = LayerMask.NameToLayer("Build");
        int decorLayer = LayerMask.NameToLayer("Decor");
        int pathLayer = LayerMask.NameToLayer("Path");

        foreach (Tilemap tm in tilemaps)
        {
            if (groundTilemap == null &&
                (tm.gameObject.layer == groundLayer || tm.gameObject.name == "Ground" || tm.gameObject.name == "GroundTilemap"))
            {
                groundTilemap = tm;
            }

            if (snowTilemap == null &&
                (tm.gameObject.layer == snowLayer || tm.gameObject.name == "Snow" || tm.gameObject.name == "SnowTilemap"))
            {
                snowTilemap = tm;
            }

            if (upperSnowTilemap == null &&
                (tm.gameObject.name == "UpperSnow" || tm.gameObject.name == "UpperSnowTilemap" || tm.gameObject.name == "SnowHillTilemap"))
            {
                upperSnowTilemap = tm;
            }

            if (waterTilemap == null &&
                (tm.gameObject.layer == waterLayer || tm.gameObject.name == "Water" || tm.gameObject.name == "WaterTilemap"))
            {
                waterTilemap = tm;
            }

            if (obstacleTilemap == null &&
                (tm.gameObject.layer == obstacleLayer || tm.gameObject.name == "Obstacle" || tm.gameObject.name == "ObstacleTilemap"))
            {
                obstacleTilemap = tm;
            }

            if (buildTilemap == null &&
                (tm.gameObject.layer == buildLayer || tm.gameObject.name == "Build" || tm.gameObject.name == "BuildTilemap"))
            {
                buildTilemap = tm;
            }

            if (decorTilemap == null &&
                (tm.gameObject.layer == decorLayer || tm.gameObject.name == "Decor" || tm.gameObject.name == "DecorTilemap"))
            {
                decorTilemap = tm;
            }

            if (pathTilemap == null &&
                (tm.gameObject.layer == pathLayer || tm.gameObject.name == "Path" || tm.gameObject.name == "PathTilemap"))
            {
                pathTilemap = tm;
            }
        }
    }

    Tilemap ResolveCellTilemap(Vector3Int cell)
    {
        if (groundTilemap != null && groundTilemap.HasTile(cell))
            return groundTilemap;

        if (snowTilemap != null && snowTilemap.HasTile(cell))
            return snowTilemap;

        if (upperSnowTilemap != null && upperSnowTilemap.HasTile(cell))
            return upperSnowTilemap;

        return groundTilemap != null ? groundTilemap : snowTilemap != null ? snowTilemap : upperSnowTilemap;
    }

    int PickWeightedCandidateIndex(List<int> candidateIndices)
    {
        int totalWeight = 0;
        for (int i = 0; i < candidateIndices.Count; i++)
            totalWeight += spawnTable[candidateIndices[i]].weight;

        if (totalWeight <= 0)
            return Random.Range(0, candidateIndices.Count);

        int random = Random.Range(0, totalWeight);
        for (int i = 0; i < candidateIndices.Count; i++)
        {
            random -= spawnTable[candidateIndices[i]].weight;
            if (random < 0)
                return i;
        }

        return candidateIndices.Count - 1;
    }

    public GameObject GetPrefab(string prefabId)
    {
        if (string.IsNullOrWhiteSpace(prefabId) || spawnTable == null)
            return null;

        for (int i = 0; i < spawnTable.Count; i++)
        {
            GameObject prefab = spawnTable[i].prefab;
            if (prefab != null && prefab.name == prefabId)
                return prefab;
        }

        return null;
    }

    public GameObject RestoreSavedObject(string prefabId, Vector3 position)
    {
        GameObject prefab = GetPrefab(prefabId);
        if (prefab == null)
            return null;

        GameObject spawnedObject = Instantiate(prefab, position, Quaternion.identity, objectsParent);
        FogObjectTint.EnsureOn(spawnedObject);
        MarkAsSaveable(spawnedObject, prefabId);
        return spawnedObject;
    }

    private static void MarkAsSaveable(GameObject worldObject, string prefabId)
    {
        SaveableWorldObject saveable = worldObject.GetComponent<SaveableWorldObject>();
        if (saveable == null)
            saveable = worldObject.AddComponent<SaveableWorldObject>();

        saveable.Initialize(prefabId);
    }

    void Shuffle(List<Vector3Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }
}
