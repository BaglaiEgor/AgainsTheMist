using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
    [SerializeField] private float holdInitialDelay = 0.2f;
    [SerializeField] private float holdRepeatInterval = 0.2f;

    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerVitals playerVitals;
    [SerializeField] private PlayerPotionEffects playerPotionEffects;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap snowTilemap;
    [SerializeField] private Tilemap upperSnowTilemap;
    [SerializeField] private Tilemap waterTilemap;
    [SerializeField] private Tilemap bridgeTilemap;
    [SerializeField] private Tilemap snowBridgeTilemap;
    [SerializeField] private Tilemap obstacleTilemap;
    [SerializeField] private Tilemap decorTilemap;
    [SerializeField] private Tilemap pathTilemap;

    [Header("Placement Preview")]
    [SerializeField] private PlacementPreviewController placementPreview;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    private float facingX = 1f;
    private bool movementLocked;
    private bool wasAttackHeld;
    private float nextHeldAttackTime;
    private float nextAttackTime;

    private readonly List<Vector3Int> placementCells = new();
    [SerializeField] private Tilemap buildTilemap;

    [Header("Tile Mining")]
    [Tooltip("Tile mining systems checked after world-object mining.")]
    [SerializeField] private List<TileMiningSystem> tileMiningSystems = new();
    [Tooltip("Random radius for spawned tile drops.")]
    [SerializeField] private float tileDropScatterRadius = 0.2f;

    [Header("Garden")]
    [SerializeField] private float defaultGardenInteractRadius = 1.5f;
    [SerializeField] private GameObject gardenBedPrefab;

    [Header("Shovel Paths")]
    [SerializeField] private TileBase grassPathRule;
    [SerializeField] private TileBase snowPathRule;

    [Header("Active Equipment")]
    [SerializeField] private CanvasGroup climbFadeCanvasGroup;
    [Min(0.05f)] [SerializeField] private float climbTransitionSeconds = 1f;
    [SerializeField] private float climbColliderCheckDistance = 0.18f;

    private bool isClimbing;
    private bool isEntryExitTeleporting;
    private Collider2D playerCollider;
    private readonly RaycastHit2D[] climbColliderHits = new RaycastHit2D[8];

    #endregion

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = gameObject.AddComponent<PlayerHealth>();
        if (playerVitals == null)
            playerVitals = GetComponent<PlayerVitals>();

        if (playerPotionEffects == null)
            playerPotionEffects = GetComponent<PlayerPotionEffects>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        playerCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        EnsurePlacementTilemaps();
        EnsureTileMiningSystems();
        EnsureBridgeGapPatches();
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
        UpdateHeldAttack();
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
        if (!CanStartAttackAction())
            return;

        PerformAttack();
        ScheduleNextHeldAttack();
    }

    void UpdateHeldAttack()
    {
        if (Mouse.current == null)
            return;

        bool isHeld = Mouse.current.leftButton.isPressed;
        if (!isHeld)
        {
            wasAttackHeld = false;
            return;
        }

        if (!wasAttackHeld)
        {
            wasAttackHeld = true;
            nextHeldAttackTime = Time.time + Mathf.Max(0.01f, holdInitialDelay);
            return;
        }

        if (Time.time < nextHeldAttackTime)
            return;

        if (!CanStartAttackAction())
        {
            ScheduleNextHeldAttack();
            return;
        }

        PerformAttack();
        ScheduleNextHeldAttack();
    }

    void ScheduleNextHeldAttack()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return;

        wasAttackHeld = true;
        nextHeldAttackTime = Mathf.Max(nextAttackTime, Time.time + GetHeldAttackInterval());
    }

    float GetHeldAttackInterval()
    {
        ItemData item = GetActiveActionItem();

        if (UsesAttackCooldown(item))
            return 1f / Mathf.Max(0.1f, item.attackSpeed);

        return Mathf.Max(0.01f, holdRepeatInterval);
    }

    bool CanStartAttackAction()
    {
        ItemData item = GetActiveActionItem();
        if (!UsesAttackCooldown(item))
            return true;

        if (Time.time < nextAttackTime)
            return false;

        nextAttackTime = Time.time + 1f / Mathf.Max(0.1f, item.attackSpeed);
        return true;
    }

    ItemData GetActiveActionItem()
    {
        if (inventoryUI != null && inventoryUI.TryGetCursorStack(out ItemData cursorItem, out _))
            return cursorItem;

        return inventory != null ? inventory.GetCurrentItem() : null;
    }

    bool UsesAttackCooldown(ItemData item)
    {
        return item != null && (item.type == ItemType.Weapon || item.type == ItemType.Tool);
    }

    void PerformAttack()
    {
        if (WorldInputBlocker.ShouldBlockWorldInput())
            return;

        if (inventoryUI != null && inventoryUI.TryGetCursorStack(out ItemData cursorItem, out _))
        {
            if (cursorItem != null)
            {
                switch (cursorItem.type)
                {
                    case ItemType.None:
                        break;

                    case ItemType.Structure:
                        PlaceStructure(cursorItem, true);
                        break;

                    case ItemType.Seed:
                        TryInteractWithGardenBed(cursorItem, true);
                        break;

                    case ItemType.Food:
                        TryUseFoodFromCursor(cursorItem);
                        break;

                    case ItemType.Consumable:
                        TryUseConsumableFromCursor(cursorItem);
                        break;
                }
            }

            return;
        }

        ItemData currentItem = inventory.GetCurrentItem();

        if (TryInteractWithGardenBed(currentItem, false))
            return;

        if (currentItem == null)
            return;

        switch (currentItem.type)
        {
            case ItemType.None:
                break;

            case ItemType.Weapon:
                UseWeapon(currentItem);
                break;

            case ItemType.Tool:
                UseTool(currentItem);
                break;

            case ItemType.Structure:
                PlaceStructure(currentItem, false);
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

            case ItemType.Food:
                UseFood(currentItem);
                break;
        }
    }

    void TryUpgradeBeacon()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (WorldInputBlocker.ShouldBlockWorldInput())
            return;

        if (InventoryCursorInputController.HandledOutsideDropThisFrame)
            return;

        if (inventoryUI != null && inventoryUI.TryGetCursorStack(out _, out _))
            return;

        if (inventory == null || Camera.main == null)
            return;

        ItemData currentItem = inventory.GetCurrentItem();
        if (TryUseToolRightClick(currentItem))
            return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            DungeonLightSource lightSource = hit.GetComponentInParent<DungeonLightSource>();
            if (lightSource != null && lightSource.TryRotate(transform))
            {
                AudioController.Instance?.PlayInteract();
                return;
            }

            DungeonLightMirror lightMirror = hit.GetComponentInParent<DungeonLightMirror>();
            if (lightMirror != null && lightMirror.TryToggleDirection(transform))
            {
                AudioController.Instance?.PlayInteract();
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Door door = hit.GetComponentInParent<Door>();
            if (door != null && door.CanInteract(transform))
            {
                door.Toggle();
                return;
            }

            KeyFence keyFence = hit.GetComponentInParent<KeyFence>();
            if (keyFence != null && keyFence.TryOpen(transform, inventory))
                return;

            EntryAndExit entryAndExit = hit.GetComponentInParent<EntryAndExit>();
            if (entryAndExit != null && !isEntryExitTeleporting && entryAndExit.TryGetTeleportPosition(transform, out _))
            {
                StartCoroutine(EntryExitRoutine(entryAndExit));
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            LighthouseKeeperNPC keeper = hit.GetComponentInParent<LighthouseKeeperNPC>();
            if (keeper != null && keeper.TryInteract(transform, inventoryUI))
            {
                AudioController.Instance?.PlayInteract();
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            PlacedLantern placedLantern = hit.GetComponentInParent<PlacedLantern>();
            if (placedLantern != null && placedLantern.TryPickup())
            {
                AudioController.Instance?.PlayInteract();
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            ChestInventory chestInventory = hit.GetComponentInParent<ChestInventory>();
            if (chestInventory != null && chestInventory.TryInteract(transform))
            {
                AudioController.Instance?.PlayInteract();
                return;
            }
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
                    continue;

                if (inventoryUI != null)
                {
                    inventoryUI.OpenFurnace(furnace);
                    AudioController.Instance?.PlayInteract();
                    return;
                }
            }

            if (station.TryInteract(transform, inventoryUI))
            {
                AudioController.Instance?.PlayInteract();
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            BeaconUpgrade beaconUpgrade = hit.GetComponentInParent<BeaconUpgrade>();
            if (beaconUpgrade == null || !beaconUpgrade.CanInteract(transform))
                continue;

            if (inventoryUI != null)
            {
                inventoryUI.OpenBeacon(beaconUpgrade);
                AudioController.Instance?.PlayInteract();
            }
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            DonationFountain fountain = hit.GetComponentInParent<DonationFountain>();
            if (fountain != null && fountain.TryDonate(transform, inventory))
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
        if (!InventoryTransactionService.TryConsume(inventory, item, 1))
            return;

        GameObject placedObject = Instantiate(item.prefab, spawnPos, Quaternion.identity);
        FogObjectTint.EnsureOn(placedObject);

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

        SaveablePlacedObject saveable = placedObject.GetComponent<SaveablePlacedObject>();
        if (saveable == null)
            saveable = placedObject.AddComponent<SaveablePlacedObject>();
        saveable.Initialize(item, 0);

        AudioController.Instance?.PlayPlace();
    }

    void UseWeapon(ItemData item)
    {
        if (item == null || item.attackPrefab == null)
            return;

        Vector2 aimDirection = GetAimDirection();
        Vector2 attackDirection = GetCardinalAttackDirection(aimDirection);
        Vector2 facingDirection = GetHorizontalAttackDirection(aimDirection);

        UpdateFacing(facingDirection.x);

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
        AudioController.Instance?.PlayWeaponSwing();
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

    Vector2 GetHorizontalAttackDirection(Vector2 aimDirection)
    {
        Vector2 source = aimDirection;
        if (source.sqrMagnitude <= 0.0001f)
            source = lastMoveDirection;
        if (source.sqrMagnitude <= 0.0001f)
            source = new Vector2(facingX, 0f);

        float x = Mathf.Abs(source.x) > 0.001f ? source.x : facingX;
        return new Vector2(Mathf.Sign(x == 0f ? 1f : x), 0f);
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

    public Vector2 GetDropFacingDirection()
    {
        if (lastMoveDirection.sqrMagnitude > 0.0001f)
            return lastMoveDirection.normalized;

        return new Vector2(Mathf.Sign(facingX == 0f ? 1f : facingX), 0f);
    }

    void OnJump()
    {
        TryUseActiveEquipment();
    }

    void TryUseActiveEquipment()
    {
        if (isClimbing || movementLocked || WorldInputBlocker.ShouldBlockWorldInput())
            return;

        EquipmentInventory equipment = inventory != null ? inventory.Equipment : null;
        ItemData activeItem = equipment != null ? equipment.GetEquippedItem(EquipmentSlotType.ActiveSlot) : null;
        if (activeItem == null || activeItem.activeEquipmentEffect != ActiveEquipmentEffectType.SnowClimb)
            return;

        TryStartSnowClimb();
    }

    bool TryStartSnowClimb()
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
            return false;

        EnsurePlacementTilemaps();
        if (upperSnowTilemap == null)
            return false;

        Vector2Int direction = GetCardinalMoveDirection(moveInput);
        Vector2 directionVector = new Vector2(direction.x, direction.y);
        if (!HasUpperCollisionInDirection(directionVector))
            return false;

        Tilemap cellTilemap = groundTilemap != null ? groundTilemap : snowTilemap != null ? snowTilemap : upperSnowTilemap;
        if (cellTilemap == null)
            return false;

        Vector3Int currentCell = cellTilemap.WorldToCell(transform.position);
        Vector3Int targetCell = currentCell + new Vector3Int(direction.x, direction.y, 0);

        if (IsOnUpperSnow(currentCell))
        {
            if (IsOnUpperSnow(targetCell) || !HasLowerSnowOrGround(targetCell))
                return false;

            StartCoroutine(SnowClimbRoutine(GetCellCenterWorld(targetCell)));
            return true;
        }

        if (!IsOnLowerSnow(currentCell) || !IsOnUpperSnow(targetCell))
            return false;

        StartCoroutine(SnowClimbRoutine(upperSnowTilemap.GetCellCenterWorld(targetCell)));
        return true;
    }

    bool HasUpperCollisionInDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        if (playerCollider == null)
            playerCollider = GetComponent<Collider2D>();

        if (playerCollider == null)
            return false;

        int hitCount = playerCollider.Cast(direction.normalized, ContactFilter2D.noFilter, climbColliderHits, Mathf.Max(0.01f, climbColliderCheckDistance));
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = climbColliderHits[i].collider;
            if (hitCollider != null && hitCollider.GetComponentInParent<Tilemap>() == upperSnowTilemap)
                return true;
        }

        return false;
    }

    bool IsOnLowerSnow(Vector3Int cell)
    {
        return snowTilemap != null && snowTilemap.HasTile(cell) && !IsOnUpperSnow(cell);
    }

    bool IsOnUpperSnow(Vector3Int cell)
    {
        return upperSnowTilemap != null && upperSnowTilemap.HasTile(cell);
    }

    bool HasLowerSnowOrGround(Vector3Int cell)
    {
        if (IsOnUpperSnow(cell))
            return false;

        return (snowTilemap != null && snowTilemap.HasTile(cell)) ||
               (groundTilemap != null && groundTilemap.HasTile(cell));
    }

    Vector2Int GetCardinalMoveDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0f ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0f ? 1 : -1);
    }

    IEnumerator SnowClimbRoutine(Vector3 targetPosition)
    {
        isClimbing = true;
        SetMovementLocked(true);
        EnsureClimbFadeCanvasGroup();

        float total = Mathf.Max(0.05f, climbTransitionSeconds);
        float fadeTime = Mathf.Max(0.02f, total * 0.35f);
        float holdTime = Mathf.Max(0f, total - fadeTime * 2f);

        yield return FadeClimbScreen(1f, fadeTime);

        AudioController.Instance?.PlaySnowClimb();

        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
        if (rb != null)
            rb.position = (Vector2)targetPosition;

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        yield return FadeClimbScreen(0f, fadeTime);

        SetMovementLocked(false);
        isClimbing = false;
    }

    IEnumerator EntryExitRoutine(EntryAndExit entryAndExit)
    {
        if (entryAndExit == null || !entryAndExit.TryGetTeleportPosition(transform, out Vector3 targetPosition))
            yield break;

        isEntryExitTeleporting = true;
        SetMovementLocked(true);
        EnsureClimbFadeCanvasGroup();

        float total = Mathf.Max(0.05f, climbTransitionSeconds);
        float fadeTime = Mathf.Max(0.02f, total * 0.35f);
        float holdTime = Mathf.Max(0f, total - fadeTime * 2f);

        yield return FadeClimbScreen(1f, fadeTime);

        AudioController.Instance?.PlayEntryExit();
        entryAndExit.ApplyDungeonState();

        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
        if (rb != null)
            rb.position = (Vector2)targetPosition;

        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        yield return FadeClimbScreen(0f, fadeTime);

        SetMovementLocked(false);
        isEntryExitTeleporting = false;
    }

    IEnumerator FadeClimbScreen(float targetAlpha, float duration)
    {
        if (climbFadeCanvasGroup == null)
            yield break;

        float startAlpha = climbFadeCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            climbFadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        climbFadeCanvasGroup.alpha = targetAlpha;
    }

    void EnsureClimbFadeCanvasGroup()
    {
        if (climbFadeCanvasGroup != null)
            return;

        GameObject canvasObject = new GameObject("ClimbFadeCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(canvasObject.transform, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        climbFadeCanvasGroup = canvasGroup;
    }

    void UseTool(ItemData item)
    {
        if (SpawnToolSwing(item))
            AudioController.Instance?.PlayToolSwing();

        if (item.toolType == ToolType.Hoe && TryPlaceGardenBedWithHoe(item))
            return;

        if (item.toolType == ToolType.Shovel && TryPlacePathWithShovel(item))
            return;

        if (TryMineWorldObject(item))
            return;

        TryMineTile(item);
    }

    bool TryUseToolRightClick(ItemData item)
    {
        if (item == null || item.type != ItemType.Tool)
            return false;

        if (item.toolType == ToolType.Hoe)
            return TryRemoveGardenBedWithHoe(item);

        if (item.toolType == ToolType.Shovel)
            return TryRemovePathWithShovel(item);

        return false;
    }

    bool TryPlaceGardenBedWithHoe(ItemData item)
    {
        if (!TryGetToolTargetCell(item, out _, out Vector3Int anchorCell))
            return false;

        GameObject prefab = GetGardenBedPrefab();
        if (prefab == null)
            return false;

        BuildPlacementCells(prefab, anchorCell);
        if (!WorldGrid.CanPlaceObject(placementCells))
            return false;

        Vector3 spawnPos = GetCellCenterWorld(anchorCell);
        GameObject gardenBed = Instantiate(prefab, spawnPos, Quaternion.identity);
        FogObjectTint.EnsureOn(gardenBed);
        AudioController.Instance?.PlayPlace();
        return true;
    }

    bool TryRemoveGardenBedWithHoe(ItemData item)
    {
        if (!TryGetToolTargetCell(item, out Vector3 mouseWorld, out _))
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
            {
                AudioController.Instance?.PlayInteract();
                return true;
            }

            if (bed.HasPlantedCrop)
                DropSeedForCrop(bed.PlantedCrop, bed.transform.position);

            bed.ClearForRemoval();
            Destroy(bed.gameObject);
            AudioController.Instance?.PlayPlace();
            return true;
        }

        return false;
    }

    bool TryPlacePathWithShovel(ItemData item)
    {
        if (!TryGetToolTargetCell(item, out _, out Vector3Int cell))
            return false;

        EnsureToolResources();

        Tilemap targetPathTilemap = pathTilemap != null ? pathTilemap : decorTilemap;
        if (targetPathTilemap == null || targetPathTilemap.HasTile(cell))
            return false;

        TileBase pathTile = null;
        if ((snowTilemap != null && snowTilemap.HasTile(cell)) ||
            (upperSnowTilemap != null && upperSnowTilemap.HasTile(cell)))
            pathTile = snowPathRule;
        else if (groundTilemap != null && groundTilemap.HasTile(cell))
            pathTile = grassPathRule;

        if (pathTile == null)
            return false;

        targetPathTilemap.SetTile(cell, pathTile);
        AudioController.Instance?.PlayPlace();
        return true;
    }

    bool TryRemovePathWithShovel(ItemData item)
    {
        if (!TryGetToolTargetCell(item, out _, out Vector3Int cell))
            return false;

        EnsureToolResources();

        Tilemap targetPathTilemap = pathTilemap != null ? pathTilemap : decorTilemap;
        if (targetPathTilemap == null)
            return false;

        TileBase tile = targetPathTilemap.GetTile(cell);
        if (tile == null || (tile != grassPathRule && tile != snowPathRule))
            return false;

        targetPathTilemap.SetTile(cell, null);
        AudioController.Instance?.PlayPlace();
        return true;
    }

    bool TryGetToolTargetCell(ItemData item, out Vector3 mouseWorld, out Vector3Int cell)
    {
        mouseWorld = default;
        cell = default;

        if (item == null || Camera.main == null || Mouse.current == null)
            return false;

        mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorld) > item.actionRadius)
            return false;

        EnsurePlacementTilemaps();
        Tilemap cellTilemap = groundTilemap != null ? groundTilemap : snowTilemap != null ? snowTilemap : upperSnowTilemap;
        if (cellTilemap == null)
            return false;

        cell = cellTilemap.WorldToCell(mouseWorld);
        return true;
    }

    Vector3 GetCellCenterWorld(Vector3Int cell)
    {
        Tilemap cellTilemap = ResolveGroundTilemap(cell);
        return cellTilemap != null ? cellTilemap.GetCellCenterWorld(cell) : Vector3.zero;
    }

    Tilemap ResolveGroundTilemap(Vector3Int cell)
    {
        if (groundTilemap != null && groundTilemap.HasTile(cell))
            return groundTilemap;

        if (snowTilemap != null && snowTilemap.HasTile(cell))
            return snowTilemap;

        if (upperSnowTilemap != null && upperSnowTilemap.HasTile(cell))
            return upperSnowTilemap;

        return groundTilemap != null ? groundTilemap : snowTilemap != null ? snowTilemap : upperSnowTilemap;
    }

    GameObject GetGardenBedPrefab()
    {
        EnsureToolResources();
        return gardenBedPrefab;
    }

    void EnsureToolResources()
    {
        if (gardenBedPrefab == null)
        {
            ItemData gardenBedItem = Resources.Load<ItemData>("Structures/Objects/GardenBed");
            if (gardenBedItem != null)
                gardenBedPrefab = gardenBedItem.prefab;
        }

        if (grassPathRule == null)
            grassPathRule = Resources.Load<TileBase>("Tiles/PathForGrassRule");

        if (snowPathRule == null)
            snowPathRule = Resources.Load<TileBase>("Tiles/PathForSnowRule");
    }

    void DropSeedForCrop(CropDefinition crop, Vector3 position)
    {
        ItemData seed = FindSeedForCrop(crop);
        if (seed == null)
            return;

        WorldItemDropService.SpawnDrop(seed, 1, position);
    }

    ItemData FindSeedForCrop(CropDefinition crop)
    {
        if (crop == null)
            return null;

        ItemData[] items = Resources.LoadAll<ItemData>("");
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item != null && item.type == ItemType.Seed && item.seedCrop == crop)
                return item;
        }

        return null;
    }

    bool TryInteractWithGardenBed(ItemData currentItem, bool consumeFromCursor)
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
            {
                AudioController.Instance?.PlayInteract();
                return true;
            }

            if (currentItem != null &&
                currentItem.type == ItemType.Seed &&
                currentItem.seedCrop != null &&
                CanConsumeGardenSeed(currentItem, consumeFromCursor) &&
                bed.TryPlant(currentItem.seedCrop))
            {
                bool consumed = TryConsumeGardenSeed(currentItem, consumeFromCursor);
                if (consumed)
                    AudioController.Instance?.PlayPlace();
                return consumed;
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
            AudioController.Instance?.PlayMine();
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
                SaveablePlacedTileRegistry.Unregister(mineResult.tilemap, mineResult.cell);
                SpawnDrops(mineResult);
                AudioController.Instance?.PlayMine();
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


    bool SpawnToolSwing(ItemData item)
    {
        if (item == null || item.attackPrefab == null)
            return false;

        Vector2 attackDirection = GetHorizontalAttackDirection(GetAimDirection());
        UpdateFacing(attackDirection.x);

        GameObject attack = Instantiate(item.attackPrefab, transform.position, Quaternion.identity);
        AttackHitbox hitbox = attack.GetComponent<AttackHitbox>();
        if (hitbox == null)
            return false;

        float swingRadius = Mathf.Clamp(item.actionRadius, 0.5f, Mathf.Max(0.5f, maxWeaponSwingRadius));

        hitbox.SetDamage(0);
        hitbox.SetOwner(playerHealth);
        hitbox.ConfigureSwing(
            attackDirection,
            transform.position,
            swingRadius,
            weaponSwingArc,
            weaponSwingDuration
        );
        return true;
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

        if (InventoryTransactionService.TryConsume(inventory, item, 1))
            AudioController.Instance?.PlayPotion();
    }

    void UseFood(ItemData item)
    {
        if (item == null || playerVitals == null)
            return;

        if (playerVitals.TryEat(item))
            AudioController.Instance?.PlayEat();
    }

    bool TryUseFoodFromCursor(ItemData item)
    {
        if (item == null || playerVitals == null || inventoryUI == null)
            return false;

        if (!inventoryUI.TryConsumeCursorItem(1))
            return false;

        bool eaten = playerVitals.TryApplyFood(item);
        if (eaten)
            AudioController.Instance?.PlayEat();
        return eaten;
    }

    bool TryUseConsumableFromCursor(ItemData item)
    {
        if (item == null || playerPotionEffects == null || inventoryUI == null)
            return false;

        if (!playerPotionEffects.TryUsePotion(item))
            return false;

        bool consumed = inventoryUI.TryConsumeCursorItem(1);
        if (consumed)
            AudioController.Instance?.PlayPotion();
        return consumed;
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

    bool PlaceStructure(ItemData item, bool consumeFromCursor)
    {
        if (!TryGetPlacementCell(item, out Vector3Int anchorCell))
            return false;

        switch (item.placementMode)
        {
            case PlacementMode.Prefab:
                return PlaceStructurePrefab(item, anchorCell, consumeFromCursor);

            case PlacementMode.Tile:
                return PlaceStructureTile(item, anchorCell, consumeFromCursor);
        }

        return false;
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

    bool PlaceStructurePrefab(ItemData item, Vector3Int anchorCell, bool consumeFromCursor)
    {
        if (item.prefab == null)
            return false;

        if (item.isDoor)
            return PlaceDoor(item, anchorCell, consumeFromCursor);

        int rotationSteps = placementPreview != null ? placementPreview.CurrentRotationSteps : 0;
        BuildPlacementCells(item.prefab, anchorCell, rotationSteps);
        if (!WorldGrid.CanPlaceObject(placementCells))
            return false;

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return false;

        Vector3 spawnPos = groundTilemap.GetCellCenterWorld(anchorCell);
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationSteps * 90f);
        GameObject placedObject = Instantiate(item.prefab, spawnPos, rotation);
        FogObjectTint.EnsureOn(placedObject);

        WorldObjectOccupier occupier = placedObject.GetComponent<WorldObjectOccupier>();
        if (occupier != null)
            occupier.SetPlacementRotationSteps(rotationSteps);

        if (placedObject.GetComponent<GardenBed>() == null && placedObject.GetComponent<PlacedLantern>() == null)
        {
            SaveablePlacedObject saveable = placedObject.GetComponent<SaveablePlacedObject>();
            if (saveable == null)
                saveable = placedObject.AddComponent<SaveablePlacedObject>();
            saveable.Initialize(item, rotationSteps);
        }

        AudioController.Instance?.PlayPlace();
        return true;
    }

    bool PlaceDoor(ItemData item, Vector3Int anchorCell, bool consumeFromCursor)
    {
        Tilemap targetTilemap = buildTilemap;
        if (targetTilemap == null || item.doorWallMarkerTile == null)
            return false;

        if (!DoorPlacementUtility.CanPlaceDoor(targetTilemap, buildTilemap, decorTilemap, anchorCell, item.doorWallMarkerTile, out Vector3Int firstDirection, out Vector3Int secondDirection))
            return false;

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return false;

        targetTilemap.SetTile(anchorCell, item.doorWallMarkerTile);
        RefreshDoorWallNeighbors(targetTilemap, anchorCell);
        RefreshDoorWallNeighbors(buildTilemap, anchorCell);
        RefreshDoorWallNeighbors(decorTilemap, anchorCell);
        WorldGrid.RegisterPlacedTile(anchorCell);
        SaveablePlacedTileRegistry.Register(item, targetTilemap, anchorCell);

        Vector3 spawnPos = groundTilemap.GetCellCenterWorld(anchorCell);
        GameObject placedObject = Instantiate(item.prefab, spawnPos, Quaternion.identity);
        FogObjectTint.EnsureOn(placedObject);

        Door door = placedObject.GetComponent<Door>();
        if (door != null)
            door.Initialize(firstDirection, secondDirection);

        SaveablePlacedObject saveable = placedObject.GetComponent<SaveablePlacedObject>();
        if (saveable == null)
            saveable = placedObject.AddComponent<SaveablePlacedObject>();

        saveable.Initialize(item, IsHorizontalDoor(firstDirection, secondDirection) ? 1 : 0);

        AudioController.Instance?.PlayPlace();
        return true;
    }

    static bool IsHorizontalDoor(Vector3Int firstDirection, Vector3Int secondDirection)
    {
        return (firstDirection == Vector3Int.left && secondDirection == Vector3Int.right) ||
               (firstDirection == Vector3Int.right && secondDirection == Vector3Int.left);
    }

    static void RefreshDoorWallNeighbors(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
            return;

        tilemap.RefreshTile(cell);
        tilemap.RefreshTile(cell + Vector3Int.left);
        tilemap.RefreshTile(cell + Vector3Int.right);
        tilemap.RefreshTile(cell + Vector3Int.up);
        tilemap.RefreshTile(cell + Vector3Int.down);
    }

    bool PlaceStructureTile(ItemData item, Vector3Int anchorCell, bool consumeFromCursor)
    {
        if (item.canPlaceOnWater)
            return PlaceWaterBridgeTile(item, anchorCell, consumeFromCursor);

        if (item.tileToPlace == null)
            return false;

        Tilemap targetTilemap = item.occupiesBuildCell ? buildTilemap : decorTilemap;
        if (targetTilemap == null)
            return false;

        // Prevent stacking multiple decor/build tiles into the same cell.
        if (targetTilemap.HasTile(anchorCell))
            return false;

        if (!WorldGrid.CanPlaceBuildTile(anchorCell))
            return false;

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return false;

        targetTilemap.SetTile(anchorCell, item.tileToPlace);

        if (item.occupiesBuildCell)
            WorldGrid.RegisterPlacedTile(anchorCell);

        SaveablePlacedTileRegistry.Register(item, targetTilemap, anchorCell);

        AudioController.Instance?.PlayPlace();
        return true;
    }

    bool PlaceWaterBridgeTile(ItemData item, Vector3Int anchorCell, bool consumeFromCursor)
    {
        Tilemap targetBridgeTilemap = GetBridgePlacementTilemap();
        if (targetBridgeTilemap == null || item.waterTileToPlace == null)
            return false;

        if (!WorldGrid.CanPlaceBridgeTile(anchorCell))
            return false;

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return false;

        targetBridgeTilemap.SetTile(anchorCell, item.waterTileToPlace);
        WorldGrid.RegisterPlacedBridge(anchorCell);
        SaveablePlacedTileRegistry.Register(item, targetBridgeTilemap, anchorCell);
        BridgeGapPatchManager.CreatePatches(anchorCell, targetBridgeTilemap);
        BridgeGapPatchManager.RefreshColliderGeometry(targetBridgeTilemap);
        AudioController.Instance?.PlayPlace();
        return true;
    }

    Tilemap GetBridgePlacementTilemap()
    {
        return bridgeTilemap != null ? bridgeTilemap : snowBridgeTilemap;
    }

    bool TryConsumePlacedItem(ItemData item, bool consumeFromCursor)
    {
        if (consumeFromCursor)
            return inventoryUI != null && inventoryUI.TryConsumeCursorItem(1);

        return inventory != null && InventoryTransactionService.TryConsume(inventory, item, 1);
    }

    bool TryConsumeGardenSeed(ItemData item, bool consumeFromCursor)
    {
        if (consumeFromCursor)
            return inventoryUI != null && inventoryUI.TryConsumeCursorItem(1);

        return inventory != null &&
               inventory.HasItem(item, 1) &&
               InventoryTransactionService.TryConsume(inventory, item, 1);
    }

    bool CanConsumeGardenSeed(ItemData item, bool consumeFromCursor)
    {
        if (consumeFromCursor)
            return inventoryUI != null && inventoryUI.TryGetCursorStack(out ItemData cursorItem, out int amount) && cursorItem == item && amount > 0;

        return inventory != null && inventory.HasItem(item, 1);
    }

    void BuildPlacementCells(GameObject prefab, Vector3Int anchorCell, int rotationSteps = 0)
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
                rotationSteps,
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
            if (snowTilemap == null)
                TryFindSnowTilemap();

            if (upperSnowTilemap == null)
                TryFindUpperSnowTilemap();

            if (waterTilemap == null)
                TryFindWaterTilemap();

            if (bridgeTilemap == null)
                TryFindBridgeTilemap();

            if (snowBridgeTilemap == null)
                TryFindSnowBridgeTilemap();

            if (decorTilemap == null)
                TryFindDecorTilemap();

            if (pathTilemap == null)
                TryFindPathTilemap();

            ConfigureWorldGrid();
            return;
        }

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int groundLayer = LayerMask.NameToLayer("Ground");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int buildLayer = LayerMask.NameToLayer("Build");
        int decorLayer = LayerMask.NameToLayer("Decor");
        int snowLayer = LayerMask.NameToLayer("Snow");
        int waterLayer = LayerMask.NameToLayer("Water");
        int pathLayer = LayerMask.NameToLayer("Path");

        foreach (var tm in tilemaps)
        {
            if (groundTilemap == null &&
                (tm.gameObject.layer == groundLayer || tm.gameObject.name == "Ground"))
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

            if (bridgeTilemap == null &&
                (tm.gameObject.name == "Bridge" || tm.gameObject.name == "BridgeTilemap"))
            {
                bridgeTilemap = tm;
            }

            if (snowBridgeTilemap == null && tm.gameObject.name == "SnowBridgeTilemap")
            {
                snowBridgeTilemap = tm;
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

            if (pathTilemap == null &&
                (tm.gameObject.layer == pathLayer || tm.gameObject.name == "Path" || tm.gameObject.name == "PathTilemap"))
            {
                pathTilemap = tm;
            }
        }

        ConfigureWorldGrid();
    }

    void TryFindSnowTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int snowLayer = LayerMask.NameToLayer("Snow");

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.layer == snowLayer || tm.gameObject.name == "Snow" || tm.gameObject.name == "SnowTilemap")
            {
                snowTilemap = tm;
                return;
            }
        }
    }

    void TryFindUpperSnowTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.name == "UpperSnow" || tm.gameObject.name == "UpperSnowTilemap" || tm.gameObject.name == "SnowHillTilemap")
            {
                upperSnowTilemap = tm;
                return;
            }
        }
    }

    void TryFindWaterTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int waterLayer = LayerMask.NameToLayer("Water");

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.layer == waterLayer || tm.gameObject.name == "Water" || tm.gameObject.name == "WaterTilemap")
            {
                waterTilemap = tm;
                return;
            }
        }
    }

    void TryFindBridgeTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.name == "Bridge" || tm.gameObject.name == "BridgeTilemap")
            {
                bridgeTilemap = tm;
                return;
            }
        }
    }

    void TryFindSnowBridgeTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.name == "SnowBridgeTilemap")
            {
                snowBridgeTilemap = tm;
                return;
            }
        }
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

    void TryFindPathTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int pathLayer = LayerMask.NameToLayer("Path");

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.layer == pathLayer || tm.gameObject.name == "Path" || tm.gameObject.name == "PathTilemap")
            {
                pathTilemap = tm;
                return;
            }
        }
    }

    void EnsureTileMiningSystems()
    {
        if (tileMiningSystems == null)
            tileMiningSystems = new List<TileMiningSystem>();

        TileMiningSystem[] found = Object.FindObjectsByType<TileMiningSystem>(FindObjectsSortMode.None);
        if (found == null || found.Length == 0)
            return;

        for (int i = 0; i < found.Length; i++)
        {
            TileMiningSystem system = found[i];
            if (system != null && !tileMiningSystems.Contains(system))
                tileMiningSystems.Add(system);
        }
    }

    void EnsureBridgeGapPatches()
    {
        BridgeGapPatchManager.RebuildForTilemap(bridgeTilemap);

        if (snowBridgeTilemap != null && snowBridgeTilemap != bridgeTilemap)
            BridgeGapPatchManager.RebuildForTilemap(snowBridgeTilemap);
    }

    void ConfigureWorldGrid()
    {
        WorldGrid.ConfigureTilemaps(
            groundTilemap,
            waterTilemap,
            obstacleTilemap,
            buildTilemap,
            snowTilemap,
            bridgeTilemap,
            snowBridgeTilemap,
            decorTilemap,
            pathTilemap,
            upperSnowTilemap
        );

        if (placementPreview != null)
        {
            placementPreview.Configure(
                inventory,
                groundTilemap,
                transform,
                inventoryUI
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
                transform,
                inventoryUI
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
