using DG.Tweening;
using UnityEngine;

public class PickupItem : MonoBehaviour
{
    private const float InventoryRefreshInterval = 0.5f;

    public ItemData itemData;
    public int amount = 1;

    [SerializeField] private float spawnImpulse = 2f;
    [SerializeField] private float attractionRadius = 1f;
    [SerializeField] private float attractionSpeed = 4f;
    [SerializeField] private float autoPickupDistance = 0.2f;
    [SerializeField] private float pickupDelay = 0.25f;
    [SerializeField] private float magnetDelay = 0.35f;
    [SerializeField] private LayerMask obstacleLayers;

    private Rigidbody2D rb;
    private SpriteRenderer[] spriteRenderers;
    private Tween popTween;
    private Tween collectTween;
    private Vector3 baseScale = Vector3.one;
    private Inventory targetInventory;
    private bool collected;
    private float spawnTime;

    private static Inventory[] cachedInventories;
    private static float nextInventoryRefreshTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseScale = transform.localScale;
    }

    private void Start()
    {
        spawnTime = Time.time;
        Vector2 force = Random.insideUnitCircle.normalized * Mathf.Max(0f, spawnImpulse);
        if (rb != null)
            rb.AddForce(force, ForceMode2D.Impulse);

        PlaySpawnPop();
    }

    private void FixedUpdate()
    {
        if (collected)
            return;
        if (!CanUseMagnet())
            return;

        targetInventory = FindClosestInventory();
        if (targetInventory == null)
            return;

        Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 target = targetInventory.transform.position;
        Vector2 toTarget = target - current;
        float distance = toTarget.magnitude;

        if (distance <= Mathf.Max(0.01f, autoPickupDistance))
        {
            TryCollect(targetInventory);
            return;
        }

        if (distance <= Mathf.Epsilon)
            return;

        Vector2 next = Vector2.MoveTowards(
            current,
            target,
            Mathf.Max(0f, attractionSpeed) * Time.fixedDeltaTime
        );

        if (rb != null)
            rb.MovePosition(next);
        else
            transform.position = next;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanPickupNow())
            return;

        Inventory inventory = other != null ? other.GetComponentInParent<Inventory>() : null;
        TryCollect(inventory);
    }

    private Inventory FindClosestInventory()
    {
        Inventory[] inventories = GetCachedInventories();
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        float maxDistance = Mathf.Max(0f, attractionRadius);
        float maxDistanceSqr = maxDistance * maxDistance;
        Inventory bestInventory = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < inventories.Length; i++)
        {
            Inventory inventory = inventories[i];
            if (inventory == null)
                continue;

            Vector2 target = inventory.transform.position;
            float distanceSqr = (target - origin).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                continue;

            if (!HasLineOfSight(origin, target))
                continue;

            bestDistanceSqr = distanceSqr;
            bestInventory = inventory;
        }

        return bestInventory;
    }

    private static Inventory[] GetCachedInventories()
    {
        if (cachedInventories == null || Time.time >= nextInventoryRefreshTime)
        {
            cachedInventories = FindObjectsByType<Inventory>(FindObjectsSortMode.None);
            nextInventoryRefreshTime = Time.time + InventoryRefreshInterval;
        }

        return cachedInventories;
    }

    private bool HasLineOfSight(Vector2 origin, Vector2 target)
    {
        if (obstacleLayers.value == 0)
            return true;

        Vector2 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction / distance, distance, obstacleLayers);
        return hit.collider == null;
    }

    private void TryCollect(Inventory inventory)
    {
        if (collected || inventory == null || itemData == null || amount <= 0 || !CanPickupNow())
            return;

        if (!InventoryTransactionService.TryInsertItem(inventory, itemData, amount))
            return;

        collected = true;
        PlayCollectPop();
    }

    private void PlaySpawnPop()
    {
        KillTween(popTween);
        transform.localScale = baseScale * 0.75f;
        popTween = transform
            .DOScale(baseScale, 0.14f)
            .SetEase(Ease.OutBack, 1.4f);
    }

    private void PlayCollectPop()
    {
        if (rb != null)
            rb.simulated = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        KillTween(collectTween);
        Sequence sequence = DOTween.Sequence();
        sequence.Join(transform.DOScale(Vector3.zero, 0.1f).SetEase(Ease.InQuad));

        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer != null)
                    sequence.Join(spriteRenderer.DOFade(0f, 0.1f));
            }
        }

        collectTween = sequence.OnComplete(() => Destroy(gameObject));
    }

    private void OnDisable()
    {
        KillTween(popTween);
        KillTween(collectTween);
    }

    private static void KillTween(Tween tween)
    {
        if (tween != null)
            tween.Kill();
    }

    private bool CanPickupNow()
    {
        return Time.time >= spawnTime + Mathf.Max(0f, pickupDelay);
    }

    private bool CanUseMagnet()
    {
        return Time.time >= spawnTime + Mathf.Max(0f, magnetDelay);
    }
}
