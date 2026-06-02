using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldObjectSpawner : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap waterTilemap;
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap buildTilemap;

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
        WorldGrid.ConfigureTilemaps(groundTilemap, waterTilemap, obstacleTilemap, buildTilemap);
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

        BoundsInt bounds = groundTilemap.cellBounds;
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
        BoundsInt bounds = groundTilemap.cellBounds;

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

            result.Add(pos);
        }

        return result;
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
        return pos.x == minX || pos.x == maxX || pos.y == minY || pos.y == maxY;
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

        if (!WorldGrid.CanPlaceObject(footprintBuffer))
            return false;

        for (int i = 0; i < footprintBuffer.Count; i++)
        {
            if (WorldGrid.HasPlacedBuildTile(footprintBuffer[i]))
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

            Vector3 worldPos = groundTilemap.GetCellCenterWorld(cell);
            Instantiate(prefab, worldPos, Quaternion.identity, objectsParent);
            return true;
        }

        return false;
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

    void Shuffle(List<Vector3Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }
}
