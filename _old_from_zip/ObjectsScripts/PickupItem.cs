using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public ItemData itemData;
    public int amount = 1;

    [SerializeField] private float spawnImpulse = 2f;
    [SerializeField] private float attractionRadius = 1f;
    [SerializeField] private float attractionSpeed = 4f;
    [SerializeField] private float autoPickupDistance = 0.2f;
    [SerializeField] private LayerMask obstacleLayers;

    private Rigidbody2D rb;
    private Inventory targetInventory;
    private bool collected;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Vector2 force = Random.insideUnitCircle.normalized * Mathf.Max(0f, spawnImpulse);
        if (rb != null)
            rb.AddForce(force, ForceMode2D.Impulse);
    }

    private void FixedUpdate()
    {
        if (collected)
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
        Inventory inventory = other != null ? other.GetComponentInParent<Inventory>() : null;
        TryCollect(inventory);
    }

    private Inventory FindClosestInventory()
    {
        Inventory[] inventories = FindObjectsByType<Inventory>(FindObjectsSortMode.None);
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
        if (collected || inventory == null || itemData == null || amount <= 0)
            return;

        inventory.Add(itemData, amount);
        collected = true;
        Destroy(gameObject);
    }
}
