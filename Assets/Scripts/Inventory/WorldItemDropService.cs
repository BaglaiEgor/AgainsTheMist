using UnityEngine;
using UnityEngine.Tilemaps;

public readonly struct ItemDropRequest
{
    public readonly Transform player;
    public readonly Vector2 facingDirection;
    public readonly ItemData item;
    public readonly int amount;

    public ItemDropRequest(Transform player, Vector2 facingDirection, ItemData item, int amount)
    {
        this.player = player;
        this.facingDirection = facingDirection;
        this.item = item;
        this.amount = amount;
    }
}

public static class WorldItemDropService
{
    private const float DropDistance = 0.75f;
    private const float ProbeRadius = 0.18f;

    public static bool TryResolveDropPosition(ItemDropRequest request, out Vector3 position)
    {
        position = default;
        if (request.player == null || request.item == null || request.amount <= 0)
            return false;

        Vector2 forward = request.facingDirection.sqrMagnitude > 0.0001f
            ? request.facingDirection.normalized
            : Vector2.right;

        Vector2 left = new Vector2(-forward.y, forward.x);
        Vector3 origin = request.player.position;

        Vector2[] offsets =
        {
            forward * DropDistance,
            forward * DropDistance + left * 0.35f,
            forward * DropDistance - left * 0.35f,
            forward * (DropDistance + 0.4f),
            forward * (DropDistance + 0.4f) + left * 0.45f,
            forward * (DropDistance + 0.4f) - left * 0.45f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 candidate = origin + new Vector3(offsets[i].x, offsets[i].y, 0f);
            candidate.z = 0f;
            if (IsValidDropPosition(candidate, request.player))
            {
                position = candidate;
                return true;
            }
        }

        Vector3 safeFallback = origin + new Vector3(forward.x, forward.y, 0f) * (DropDistance + 0.8f);
        safeFallback.z = 0f;
        if (IsValidDropPosition(safeFallback, request.player))
        {
            position = safeFallback;
            return true;
        }

        return false;
    }

    public static GameObject SpawnDrop(ItemData item, int amount, Vector3 position)
    {
        if (item == null || amount <= 0)
            return null;

        GameObject spawnedFromPrefab = ResourceDrop.SpawnPickup(item, amount, position);
        if (spawnedFromPrefab != null)
            return spawnedFromPrefab;

        // Fallback path keeps runtime drop functional until every ItemData has pickupPrefab assigned.
        return SpawnRuntimePickup(item, amount, position);
    }

    private static bool IsValidDropPosition(Vector3 position, Transform player)
    {
        if (UiInputBlocker.IsPointerOverBlockingUI())
            return false;

        if (WorldGrid.TryGetGroundCell(position, out Vector3Int cell))
        {
            WorldGridBlockReason reason = WorldGrid.GetObjectPlacementBlockReason(cell);
            if (reason != WorldGridBlockReason.None)
                return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, ProbeRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (player != null && hit.transform.IsChildOf(player))
                continue;

            if (hit.GetComponentInParent<PickupItem>() != null)
                continue;

            if (hit.GetComponentInParent<ChestInventory>() != null ||
                hit.GetComponentInParent<CraftingStation>() != null ||
                hit.GetComponentInParent<BeaconUpgrade>() != null ||
                hit.GetComponentInParent<FurnaceStation>() != null)
            {
                return false;
            }

            if (!hit.isTrigger)
                return false;
        }

        return true;
    }

    private static GameObject SpawnRuntimePickup(ItemData item, int amount, Vector3 position)
    {
        GameObject dropObject = new GameObject($"Drop_{item.itemID}");
        dropObject.transform.position = position;

        SpriteRenderer renderer = dropObject.AddComponent<SpriteRenderer>();
        renderer.sprite = item.icon;
        renderer.sortingOrder = 100;

        CircleCollider2D collider = dropObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.18f;

        Rigidbody2D body = dropObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 5f;

        PickupItem pickup = dropObject.AddComponent<PickupItem>();
        pickup.itemData = item;
        pickup.amount = amount;
        return dropObject;
    }
}
