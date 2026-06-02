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
    private Inventory targetInventory;
    private bool collected;
    private float spawnTime;

    private static Inventory[] cachedInventories;
    private static float nextInventoryRefreshTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time;
        Vector2 force = Random.insideUnitCircle.normalized * Mathf.Max(0f, spawnImpulse);
        if (rb != null)
            rb.AddForce(force, ForceMode2D.Impulse);
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
        Destroy(gameObject);
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
