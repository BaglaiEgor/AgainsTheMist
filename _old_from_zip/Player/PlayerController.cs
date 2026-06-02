using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{
    private const string ToolHitColliderName = "HitBox";

    #region fields
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runParamName = "IsRunning";

    [Header("Weapon Attack")]
    [SerializeField] private float weaponSwingArc = 110f;
    [SerializeField] private float weaponSwingDuration = 0.15f;
    [SerializeField] private float maxWeaponSwingRadius = 1.8f;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerPotionEffects playerPotionEffects;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap decorTilemap;

    [Header("Placement Preview")]
    [SerializeField] private PlacementPreviewController placementPreview;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    private float facingX = 1f;
    private bool movementLocked;

    private readonly List<Vector3Int> placementCells = new();
    [SerializeField] private Tilemap buildTilemap;

    [Header("Tile Mining")]
    [Tooltip("Tile mining systems checked after world-object mining.")]
    [SerializeField] private List<TileMiningSystem> tileMiningSystems = new();
    [Tooltip("Random radius for spawned tile drops.")]
    [SerializeField] private float tileDropScatterRadius = 0.2f;

    [Header("Garden")]
    [SerializeField] private float defaultGardenInteractRadius = 1.5f;

    #endregion

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = gameObject.AddComponent<PlayerHealth>();

        if (playerPotionEffects == null)
            playerPotionEffects = GetComponent<PlayerPotionEffects>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        EnsurePlacementTilemaps();
        EnsureTileMiningSystems();
        EnsurePlacementPreviewController();
    }

    #region move
    void FixedUpdate()
    {
        if (movementLocked)
            return;

        rb.MovePosition(rb.position + moveSpeed * Time.fixedDeltaTime * moveInput);
    }

    void Update()
    {
        EnsurePlacementPreviewController();
        UpdateAnimationState();
        TryUpgradeBeacon();
    }

    void OnMove(InputValue value)
    {
        if (movementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        Vector2 rawInput = value.Get<Vector2>();
        moveInput = rawInput.normalized;

        if (rawInput.sqrMagnitude > 0.0001f)
            lastMoveDirection = moveInput;

        UpdateFacing(moveInput.x);
    }
    #endregion

    #region animation
    void UpdateFacing(float horizontalInput)
    {
        if (horizontalInput == 0f)
            return;

        facingX = Mathf.Sign(horizontalInput);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingX;
        transform.localScale = scale;
    }

    void UpdateAnimationState()
    {
        if (animator == null)
            return;

        bool isRunning = moveInput.sqrMagnitude > 0.0001f;
        animator.SetBool(runParamName, isRunning);
    }
    #endregion

    #region attack
    void OnAttack()
    {
        ItemData currentItem = inventory.GetCurrentItem();

        if (TryInteractWithGardenBed(currentItem))
            return;

        if (currentItem == null)
            return;

        switch (currentItem.type)
        {
            case ItemType.Weapon:
                UseWeapon(currentItem);
                break;

            case ItemType.Tool:
                UseTool(currentItem);
                break;

            case ItemType.Structure:
                PlaceStructure(currentItem);
                break;

            case ItemType.Seed:
                break;

            case ItemType.Material:
                break;

            case ItemType.Lantern:
                PlaceLantern(currentItem);
                break;

            case ItemType.Equipment:
                break;

            case ItemType.Consumable:
                UseConsumable(currentItem);
                break;
        }
    }

    void TryUpgradeBeacon()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (inventory == null || Camera.main == null)
            return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlacedLantern placedLantern = hit.GetComponentInParent<PlacedLantern>();
            if (placedLantern != null && placedLantern.TryPickup())
                return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            ChestInventory chestInventory = hit.GetComponentInParent<ChestInventory>();
            if (chestInventory != null && chestInventory.TryInteract(transform))
                return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            CraftingStation station = hit.GetComponentInParent<CraftingStation>();
            if (station == null || !station.CanInteract(transform))
                continue;

            if (station.StationType == CraftStationType.Furnace)
            {
                FurnaceStation furnace = station.GetComponent<FurnaceStation>();
                if (furnace == null)
                    furnace = station.gameObject.AddComponent<FurnaceStation>();

                if (inventoryUI != null)
                {
                    inventoryUI.OpenFurnace(furnace);
                    return;
                }
            }

            if (station.TryInteract(transform, inventoryUI))
                return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            BeaconUpgrade beaconUpgrade = hit.GetComponentInParent<BeaconUpgrade>();
            if (beaconUpgrade == null)
                continue;

            if (inventoryUI != null)
                inventoryUI.OpenBeacon(beaconUpgrade);
            return;
        }
    }

    void PlaceLantern(ItemData item)
    {
        if (item == null || inventory == null || item.prefab == null)
            return;

        if (!inventory.HasItem(item, 1))
            return;

        if (!TryGetPlacementCell(item, out Vector3Int anchorCell))
            return;

        BuildPlacementCells(item.prefab, anchorCell);
        if (!WorldGrid.CanPlaceObject(placementCells))
            return;

        Vector3 spawnPos = groundTilemap.GetCellCenterWorld(anchorCell);
        GameObject placedObject = Instantiate(item.prefab, spawnPos, Quaternion.identity);

        PlacedLantern placedLantern = placedObject.GetComponent<PlacedLantern>();
        if (placedLantern == null)
            placedLantern = placedObject.AddComponent<PlacedLantern>();

        float maxCharge = Mathf.Max(1f, item.lanternMaxCharge);
        float initialCharge = maxCharge;
        if (PlayerLantern.TryGetLanternCharge(item, out float currentCharge, out float currentMaxCharge))
        {
            maxCharge = Mathf.Max(1f, currentMaxCharge);
            initialCharge = Mathf.Clamp(currentCharge, 0f, maxCharge);
        }

        placedLantern.Initialize(item, initialCharge, maxCharge, item.lanternDrainPerSecond);
        inventory.Remove(item, 1);
    }

    void UseWeapon(ItemData item)
    {
        Vector2 aimDirection = GetAimDirection();
        Vector2 attackDirection = GetCardinalAttackDirection(aimDirection);

        if (Mathf.Abs(aimDirection.x) > 0.001f)
            UpdateFacing(aimDirection.x);

        GameObject attack = Instantiate(item.attackPrefab, transform.position, Quaternion.identity);

        AttackHitbox hitbox = attack.GetComponent<AttackHitbox>();
        if (hitbox == null)
            return;

        float swingRadius = Mathf.Clamp(item.actionRadius, 0.5f, Mathf.Max(0.5f, maxWeaponSwingRadius));

        hitbox.SetDamage(item.damage);
        hitbox.SetOwner(playerHealth);
        hitbox.ConfigureSwing(
            attackDirection,
            transform.position,
            swingRadius,
            weaponSwingArc,
            weaponSwingDuration
        );
    }

    Vector2 GetCardinalAttackDirection(Vector2 aimDirection)
    {
        Vector2 source = aimDirection;
        if (source.sqrMagnitude <= 0.0001f)
            source = lastMoveDirection;
        if (source.sqrMagnitude <= 0.0001f)
            source = new Vector2(facingX, 0f);

        if (Mathf.Abs(source.x) >= Mathf.Abs(source.y))
            return new Vector2(Mathf.Sign(source.x == 0f ? facingX : source.x), 0f);

        return new Vector2(0f, Mathf.Sign(source.y));
    }

    Vector2 GetAimDirection()
    {
        if (Camera.main != null && Mouse.current != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorld.z = transform.position.z;

            Vector2 toCursor = (Vector2)(mouseWorld - transform.position);
            if (toCursor.sqrMagnitude > 0.0001f)
                return toCursor.normalized;
        }

        return lastMoveDirection.sqrMagnitude > 0.0001f
            ? lastMoveDirection
            : new Vector2(facingX, 0f);
    }

    void UseTool(ItemData item)
    {
        SpawnToolSwing(item);

        if (TryMineWorldObject(item))
            return;

        TryMineTile(item);
    }

    bool TryInteractWithGardenBed(ItemData currentItem)
    {
        if (Camera.main == null || Mouse.current == null)
            return false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        mouseWorld.z = 0f;

        float interactionRadius = GetGardenInteractionRadius(currentItem);
        if (interactionRadius > 0f &&
            Vector2.Distance(transform.position, mouseWorld) > interactionRadius)
            return false;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            GardenBed bed = hit.GetComponentInParent<GardenBed>();
            if (bed == null)
                continue;

            if (bed.TryHarvest())
                return true;

            if (currentItem != null &&
                currentItem.type == ItemType.Seed &&
                currentItem.seedCrop != null &&
                bed.TryPlant(currentItem.seedCrop))
            {
                if (inventory != null)
                    inventory.Remove(currentItem, 1);

                return true;
            }
        }

        return false;
    }

    float GetGardenInteractionRadius(ItemData currentItem)
    {
        if (currentItem != null && currentItem.actionRadius > 0f)
            return currentItem.actionRadius;

        return Mathf.Max(0f, defaultGardenInteractRadius);
    }

    bool TryMineWorldObject(ItemData item)
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        mouseWorld.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorld) > item.actionRadius)
            return false;

        bool encounteredWorldObject = false;
        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        foreach (var hit in hits)
        {
            if (!IsValidToolHitCollider(hit))
                continue;

            WorldObject obj = hit.GetComponentInParent<WorldObject>();
            if (!obj)
                continue;

            encounteredWorldObject = true;

            if (obj.requiredTool != ToolType.None && obj.requiredTool != item.toolType)
                continue;

            if (obj.requiredTool != ToolType.None && item.toolPower < obj.minToolPower)
                continue;

            obj.TakeDamage(item.toolPower);
            return true;
        }

        return encounteredWorldObject;
    }

    bool TryMineTile(ItemData item)
    {
        EnsureTileMiningSystems();
        if (tileMiningSystems == null || tileMiningSystems.Count == 0)
            return false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );
        mouseWorld.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorld) > item.actionRadius)
            return false;

        for (int i = 0; i < tileMiningSystems.Count; i++)
        {
            TileMiningSystem system = tileMiningSystems[i];
            if (system == null)
                continue;

            bool mined = system.TryMineAtWorld(mouseWorld, item, out TileMineResult mineResult);
            if (mined)
            {
                SpawnDrops(mineResult);
                return true;
            }

            // If we targeted a mineable tile but tool is wrong/weak, consume action.
            if (mineResult.status == TileMineStatus.WrongTool ||
                mineResult.status == TileMineStatus.NotEnoughPower)
            {
                return true;
            }
        }

        return false;
    }

    bool IsValidToolHitCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        string colliderName = collider.gameObject.name;
        return colliderName == ToolHitColliderName;
    }


    void SpawnToolSwing(ItemData item)
    {
        // Placeholder for a future tool swing animation/hitbox.
    }

    void UseConsumable(ItemData item)
    {
        if (item == null || inventory == null)
            return;

        if (!inventory.HasItem(item, 1))
            return;

        if (playerPotionEffects == null)
            return;

        if (!playerPotionEffects.TryUsePotion(item))
            return;

        inventory.Remove(item, 1);
    }

    void SpawnDrops(TileMineResult mineResult)
    {
        Drop[] drops = mineResult.drops;
        if (drops == null || drops.Length == 0)
            return;

        for (int i = 0; i < drops.Length; i++)
        {
            Drop drop = drops[i];
            if (drop == null || drop.prefab == null)
                continue;

            if (Random.value > Mathf.Clamp01(drop.chance))
                continue;

            int minAmount = Mathf.Max(0, drop.minAmount);
            int maxAmount = Mathf.Max(minAmount, drop.maxAmount);
            int count = Random.Range(minAmount, maxAmount + 1);

            for (int j = 0; j < count; j++)
            {
                Vector3 spawnPos = mineResult.worldPosition;
                if (tileDropScatterRadius > 0f)
                {
                    Vector2 offset = Random.insideUnitCircle * tileDropScatterRadius;
                    spawnPos += new Vector3(offset.x, offset.y, 0f);
                }

                Instantiate(drop.prefab, spawnPos, Quaternion.identity);
            }
        }
    }

    void PlaceStructure(ItemData item)
    {
        if (!TryGetPlacementCell(item, out Vector3Int anchorCell))
            return;

        switch (item.placementMode)
        {
            case PlacementMode.Prefab:
                PlaceStructurePrefab(item, anchorCell);
                break;

            case PlacementMode.Tile:
                PlaceStructureTile(item, anchorCell);
                break;
        }
    }
    
    bool TryGetPlacementCell(ItemData item, out Vector3Int anchorCell)
    {
        anchorCell = default;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorld) > item.actionRadius)
            return false;

        EnsurePlacementTilemaps();
        if (groundTilemap == null)
            return false;

        anchorCell = groundTilemap.WorldToCell(mouseWorld);
        return true;
    }

    void PlaceStructurePrefab(ItemData item, Vector3Int anchorCell)
    {
        if (item.prefab == null)
            return;

        BuildPlacementCells(item.prefab, anchorCell);
        if (!WorldGrid.CanPlaceObject(placementCells))
            return;

        Vector3 spawnPos = groundTilemap.GetCellCenterWorld(anchorCell);
        Instantiate(item.prefab, spawnPos, Quaternion.identity);
        inventory.Remove(item, 1);
    }

    void PlaceStructureTile(ItemData item, Vector3Int anchorCell)
    {
        if (item.tileToPlace == null)
            return;

        Tilemap targetTilemap = item.occupiesBuildCell ? buildTilemap : decorTilemap;
        if (targetTilemap == null)
            return;

        // Prevent stacking multiple decor/build tiles into the same cell.
        if (targetTilemap.HasTile(anchorCell))
            return;

        if (item.occupiesBuildCell && !WorldGrid.CanPlaceBuildTile(anchorCell))
            return;

        targetTilemap.SetTile(anchorCell, item.tileToPlace);

        if (item.occupiesBuildCell)
            WorldGrid.RegisterPlacedTile(anchorCell);

        inventory.Remove(item, 1);
    }

    void BuildPlacementCells(GameObject prefab, Vector3Int anchorCell)
    {
        placementCells.Clear();

        WorldObjectOccupier occupier = prefab.GetComponent<WorldObjectOccupier>();
        if (occupier != null)
        {
            WorldObjectOccupier.BuildFootprintCells(
                anchorCell,
                occupier.width,
                occupier.height,
                occupier.CenterOnTransform,
                placementCells
            );
        }
        else
        {
            placementCells.Add(anchorCell);
        }
    }

    void EnsurePlacementTilemaps()
    {
        if (groundTilemap != null && buildTilemap != null)
        {
            if (decorTilemap == null)
                TryFindDecorTilemap();

            ConfigureWorldGrid();
            return;
        }

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int buildLayer = LayerMask.NameToLayer("Build");
        int decorLayer = LayerMask.NameToLayer("Decor");

        foreach (var tm in tilemaps)
        {
            if (groundTilemap == null &&
                (tm.gameObject.layer == groundLayer || tm.gameObject.name == "Ground"))
            {
                groundTilemap = tm;
            }

            if (obstacleTilemap == null &&
                (tm.gameObject.layer == obstacleLayer || tm.gameObject.name == "Obstacle"))
            {
                obstacleTilemap = tm;
            }

            if (buildTilemap == null &&
                (tm.gameObject.layer == buildLayer || tm.gameObject.name == "Build"))
            {
                buildTilemap = tm;
            }

            if (decorTilemap == null &&
                (tm.gameObject.layer == decorLayer || tm.gameObject.name == "Decor"))
            {
                decorTilemap = tm;
            }
        }

        ConfigureWorldGrid();
    }

    void TryFindDecorTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int decorLayer = LayerMask.NameToLayer("Decor");

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.layer == decorLayer || tm.gameObject.name == "Decor")
            {
                decorTilemap = tm;
                return;
            }
        }
    }

    void EnsureTileMiningSystems()
    {
        if (tileMiningSystems != null && tileMiningSystems.Count > 0)
            return;

        TileMiningSystem[] found = Object.FindObjectsByType<TileMiningSystem>(FindObjectsSortMode.None);
        if (found == null || found.Length == 0)
            return;

        tileMiningSystems = new List<TileMiningSystem>(found);
    }

    void ConfigureWorldGrid()
    {
        WorldGrid.ConfigureTilemaps(
            groundTilemap,
            null,
            obstacleTilemap,
            buildTilemap
        );

        if (placementPreview != null)
        {
            placementPreview.Configure(
                inventory,
                groundTilemap,
                transform
            );
        }
    }

    void EnsurePlacementPreviewController()
    {
        if (placementPreview == null)
            placementPreview = GetComponent<PlacementPreviewController>();

        if (placementPreview != null)
        {
            placementPreview.Configure(
                inventory,
                groundTilemap,
                transform
            );
        }
    }

    #endregion

    #region inventory
    void OnScroll(InputValue value)
    {
        Vector2 scroll = value.Get<Vector2>();
        if (scroll.y > 0)
            inventory.SelectPreviousSlot();
        else if (scroll.y < 0)
            inventory.SelectNextSlot();
    }

    void OnSelectSlot1() => inventory.SetActiveSlot(0);
    void OnSelectSlot2() => inventory.SetActiveSlot(1);
    void OnSelectSlot3() => inventory.SetActiveSlot(2);
    void OnSelectSlot4() => inventory.SetActiveSlot(3);
    void OnSelectSlot5() => inventory.SetActiveSlot(4);
    void OnSelectSlot6() => inventory.SetActiveSlot(5);
    void OnSelectSlot7() => inventory.SetActiveSlot(6);
    void OnSelectSlot8() => inventory.SetActiveSlot(7);
    void OnSelectSlot9() => inventory.SetActiveSlot(8);

    void OnInventory()
    {
        inventoryUI.Toggle();
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (movementLocked)
            moveInput = Vector2.zero;
    }
    #endregion
}
