using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerSnowTrail : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject trailPrefab;

    [Header("Snow Check")]
    [SerializeField] private Tilemap snowTilemap;
    [SerializeField] private LayerMask snowLayer;
    [SerializeField] private float snowCheckRadius = 0.12f;

    [Header("Spawn")]
    [SerializeField] private float spawnDistance = 0.35f;
    [SerializeField] private float standingSpawnInterval = 1.2f;
    [SerializeField] private float minMoveDistance = 0.02f;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private float rotationOffset = -90f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private int maxActiveTrails = 80;

    private readonly Collider2D[] snowHits = new Collider2D[4];
    private readonly Queue<GameObject> activeTrails = new();

    private Vector3 lastPosition;
    private Vector3 lastSpawnPosition;
    private Vector2 lastMoveDirection = Vector2.down;
    private GameObject currentMovingTrail;
    private GameObject currentStandingTrail;
    private float nextStandingSpawnTime;
    private bool hasSpawnedTrail;

    void Awake()
    {
        if (snowLayer.value == 0)
        {
            int snowLayerIndex = LayerMask.NameToLayer("Snow");
            if (snowLayerIndex >= 0)
                snowLayer = 1 << snowLayerIndex;
        }

        lastPosition = transform.position;
        lastSpawnPosition = transform.position;
    }

    void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector3 positionDelta = currentPosition - lastPosition;
        bool isMoving = positionDelta.sqrMagnitude >= minMoveDistance * minMoveDistance;

        if (isMoving)
            lastMoveDirection = positionDelta.normalized;

        if (!IsOnSnow(currentPosition))
        {
            hasSpawnedTrail = false;
            currentMovingTrail = null;
            ReleaseStandingTrail();
            lastPosition = currentPosition;
            return;
        }

        if (isMoving)
            TrySpawnMovingTrail(currentPosition);
        else
            TrySpawnStandingTrail(currentPosition);

        lastPosition = currentPosition;
    }

    private void TrySpawnMovingTrail(Vector3 currentPosition)
    {
        ReleaseStandingTrail();

        if (!hasSpawnedTrail || Vector2.Distance(currentPosition, lastSpawnPosition) >= spawnDistance)
        {
            currentMovingTrail = SpawnTrail(currentPosition, true);
            return;
        }

        MoveTrailTo(currentMovingTrail, currentPosition, true);
    }

    private void TrySpawnStandingTrail(Vector3 currentPosition)
    {
        currentMovingTrail = null;

        if (currentStandingTrail == null)
            currentStandingTrail = SpawnTrail(currentPosition, false, true);
        else
            MoveTrailTo(currentStandingTrail, currentPosition, false);
    }

    private GameObject SpawnTrail(Vector3 position, bool rotateByMovement)
    {
        return SpawnTrail(position, rotateByMovement, false);
    }

    private GameObject SpawnTrail(Vector3 position, bool rotateByMovement, bool holdUntilReleased)
    {
        if (trailPrefab == null)
            return null;

        GameObject trail = Instantiate(trailPrefab);
        MoveTrailTo(trail, position, rotateByMovement);

        SnowTrailStamp stamp = trail.GetComponent<SnowTrailStamp>();
        if (stamp != null)
            stamp.Initialize(lifetime, fadeTime, holdUntilReleased);

        activeTrails.Enqueue(trail);
        TrimActiveTrails();

        hasSpawnedTrail = true;
        lastSpawnPosition = position;
        nextStandingSpawnTime = Time.time + Mathf.Max(0.05f, standingSpawnInterval);

        return trail;
    }

    private void ReleaseStandingTrail()
    {
        if (currentStandingTrail == null)
            return;

        SnowTrailStamp stamp = currentStandingTrail.GetComponent<SnowTrailStamp>();
        if (stamp != null)
            stamp.Release();

        currentStandingTrail = null;
    }

    private void MoveTrailTo(GameObject trail, Vector3 position, bool rotateByMovement)
    {
        if (trail == null)
            return;

        trail.transform.position = position + spawnOffset;
        trail.transform.rotation = rotateByMovement ? GetMovementRotation() : Quaternion.identity;
    }

    private Quaternion GetMovementRotation()
    {
        float angle = Mathf.Atan2(lastMoveDirection.y, lastMoveDirection.x) * Mathf.Rad2Deg + rotationOffset;
        return Quaternion.Euler(0f, 0f, angle);
    }

    private void TrimActiveTrails()
    {
        int safeLimit = Mathf.Max(1, maxActiveTrails);
        while (activeTrails.Count > safeLimit)
        {
            GameObject oldestTrail = activeTrails.Dequeue();
            if (oldestTrail != null)
                Destroy(oldestTrail);
        }
    }

    private bool IsOnSnow(Vector3 worldPosition)
    {
        if (snowTilemap != null)
        {
            Vector3Int cell = snowTilemap.WorldToCell(worldPosition);
            if (snowTilemap.HasTile(cell))
                return true;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(snowLayer);
        filter.useTriggers = true;

        return Physics2D.OverlapCircle(worldPosition, snowCheckRadius, filter, snowHits) > 0;
    }
}
