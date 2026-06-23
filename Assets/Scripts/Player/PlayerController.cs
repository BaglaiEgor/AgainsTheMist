using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    [Min(0.05f)] [SerializeField] private float weaponSwingRadius = 0.7f;
    [SerializeField] private float holdInitialDelay = 0.2f;
    [SerializeField] private float holdRepeatInterval = 0.2f;
    [Tooltip("Optional point on player where weapon/tool attack prefab starts. If empty, player position is used.")]
    [SerializeField] private Transform attackSpawnPoint;

    [Header("Attack Feedback")]
    [SerializeField] private Vector3 attackPunchScale = new Vector3(0.1f, -0.06f, 0f);
    [Min(0.01f)] [SerializeField] private float attackPunchDuration = 0.1f;

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
    [Min(0.05f)] [SerializeField] private float dungeonTransitionSeconds = 2f;
    [SerializeField] private float climbColliderCheckDistance = 0.18f;

    [Header("Active Equipment Feedback")]
    [SerializeField] private Color dashGhostColor = new Color(0.6f, 0.9f, 1f, 0.45f);
    [SerializeField] private Color medkitFlashColor = new Color(0.35f, 1f, 0.45f, 0.35f);
    [SerializeField] private Color shieldColor = new Color(0.35f, 0.75f, 1f, 0.35f);
    [Tooltip("Optional point where the active shield visual is created. If empty, the player center is used.")]
    [SerializeField] private Transform shieldVisualSpawnPoint;

    private bool isClimbing;
    private bool isDashing;
    private bool sprintRecoveryLocked;
    private bool wasActiveEquipmentButtonHeld;
    private bool isEntryExitTeleporting;
    private Collider2D playerCollider;
    private readonly RaycastHit2D[] climbColliderHits = new RaycastHit2D[8];
    private float dashCooldownRemaining;
    private float medkitCooldownRemaining;
    private float shieldCooldownRemaining;
    private float shieldRemaining;
    private float currentStamina;
    private ItemData dashCooldownItem;
    private ItemData medkitCooldownItem;
    private ItemData shieldCooldownItem;
    private ItemData activeShieldItem;
    private ItemData lastActiveEquipmentItem;
    private SpriteRenderer cachedVisualRenderer;
    private GameObject shieldVisual;
    private Tween attackPunchTween;

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
        if (movementLocked || isDashing)
            return;

        rb.MovePosition(rb.position + GetCurrentMoveSpeed() * Time.fixedDeltaTime * moveInput);
    }

    void Update()
    {
        EnsurePlacementPreviewController();
        UpdateActiveEquipmentState();
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

            CreepyFinalAltarController finalAltar = hit.GetComponentInParent<CreepyFinalAltarController>();
            if (finalAltar != null && finalAltar.TryUseAltar())
            {
                AudioController.Instance?.PlayInteract();
                return;
            }

            CreepySealEncounter sealEncounter = hit.GetComponentInParent<CreepySealEncounter>();
            if (sealEncounter != null && sealEncounter.TryActivate())
            {
                AudioController.Instance?.PlayInteract();
                return;
            }

            CreepyClickableActivator clickableActivator = hit.GetComponentInParent<CreepyClickableActivator>();
            if (clickableActivator != null && clickableActivator.TryActivate())
            {
                AudioController.Instance?.PlayInteract();
                return;
            }

            CreepySequencePuzzleButton puzzleButton = hit.GetComponentInParent<CreepySequencePuzzleButton>();
            if (puzzleButton != null && puzzleButton.TryPress())
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

        Vector3 attackOrigin = GetAttackOrigin();
        Transform swingPivot = attackSpawnPoint != null ? attackSpawnPoint : transform;
        GameObject attack = Instantiate(item.attackPrefab, attackOrigin, Quaternion.identity, transform);

        AttackHitbox hitbox = attack.GetComponent<AttackHitbox>();
        if (hitbox == null)
            return;

        float swingRadius = weaponSwingRadius;

        hitbox.SetDamage(item.damage);
        hitbox.SetOwner(playerHealth);
        hitbox.ConfigureSwing(
            attackDirection,
            swingPivot,
            swingRadius,
            weaponSwingArc,
            weaponSwingDuration
        );
        PlayAttackPunch();
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

    public float CurrentStamina => currentStamina;
    public float MaxStamina => GetSprintBootsItem() != null ? Mathf.Max(1f, GetSprintBootsItem().maxStamina) : 0f;
    public bool HasSprintBootsEquipped => GetSprintBootsItem() != null;
    public float RemainingDashCooldown => dashCooldownRemaining;
    public float RemainingMedkitCooldown => medkitCooldownRemaining;
    public float RemainingShieldCooldown => shieldCooldownRemaining;
    public float RemainingShieldTime => shieldRemaining;
    public ItemData ActiveDashCooldownItem => dashCooldownRemaining > 0f ? dashCooldownItem : null;
    public ItemData ActiveMedkitCooldownItem => medkitCooldownRemaining > 0f ? medkitCooldownItem : null;
    public ItemData ActiveShieldCooldownItem => shieldCooldownRemaining > 0f ? shieldCooldownItem : null;
    public ItemData ActiveShieldItem => shieldRemaining > 0f ? activeShieldItem : null;

    void OnJump()
    {
        TryUseActiveEquipment();
    }

    void OnSprint(InputValue value)
    {
        // Sprint is intentionally handled through Space by active SprintBoots.
    }

    void TryUseActiveEquipment()
    {
        if (isClimbing || movementLocked || WorldInputBlocker.ShouldBlockWorldInput())
            return;

        EquipmentInventory equipment = inventory != null ? inventory.Equipment : null;
        ItemData activeItem = equipment != null ? equipment.GetEquippedItem(EquipmentSlotType.ActiveSlot) : null;
        if (activeItem == null)
            return;

        switch (activeItem.activeEquipmentEffect)
        {
            case ActiveEquipmentEffectType.SnowClimb:
                TryStartSnowClimb();
                break;
            case ActiveEquipmentEffectType.Dash:
                TryStartDash(activeItem);
                break;
            case ActiveEquipmentEffectType.Medkit:
                TryUseMedkit(activeItem);
                break;
            case ActiveEquipmentEffectType.Shield:
                TryUseShield(activeItem);
                break;
        }
    }

    void UpdateActiveEquipmentState()
    {
        float delta = Time.deltaTime;

        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - delta);
        if (dashCooldownRemaining <= 0f)
            dashCooldownItem = null;

        if (medkitCooldownRemaining > 0f)
            medkitCooldownRemaining = Mathf.Max(0f, medkitCooldownRemaining - delta);
        if (medkitCooldownRemaining <= 0f)
            medkitCooldownItem = null;

        if (shieldCooldownRemaining > 0f)
            shieldCooldownRemaining = Mathf.Max(0f, shieldCooldownRemaining - delta);
        if (shieldCooldownRemaining <= 0f)
            shieldCooldownItem = null;

        if (shieldRemaining > 0f)
            shieldRemaining = Mathf.Max(0f, shieldRemaining - delta);
        if (shieldRemaining <= 0f)
            activeShieldItem = null;

        SetShieldVisualVisible(shieldRemaining > 0f);
        UpdateSprintStamina(delta);
    }

    void UpdateSprintStamina(float delta)
    {
        ItemData sprintBoots = GetSprintBootsItem();
        if (sprintBoots != lastActiveEquipmentItem)
        {
            lastActiveEquipmentItem = sprintBoots;
            currentStamina = sprintBoots != null ? Mathf.Max(1f, sprintBoots.maxStamina) : 0f;
            sprintRecoveryLocked = false;
            wasActiveEquipmentButtonHeld = false;
        }

        if (sprintBoots == null)
        {
            currentStamina = 0f;
            sprintRecoveryLocked = false;
            wasActiveEquipmentButtonHeld = false;
            return;
        }

        float maxStamina = Mathf.Max(1f, sprintBoots.maxStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        bool activeButtonHeld = IsActiveEquipmentButtonHeld();

        if (wasActiveEquipmentButtonHeld && !activeButtonHeld && currentStamina < maxStamina)
            sprintRecoveryLocked = true;

        if (IsSprintActive())
        {
            currentStamina = Mathf.Max(0f, currentStamina - Mathf.Max(0f, sprintBoots.staminaDrainPerSecond) * delta);
            if (currentStamina <= 0f)
                sprintRecoveryLocked = true;

            wasActiveEquipmentButtonHeld = activeButtonHeld;
            return;
        }

        currentStamina = Mathf.Min(maxStamina, currentStamina + Mathf.Max(0f, sprintBoots.staminaRegenPerSecond) * delta);
        if (currentStamina >= maxStamina)
            sprintRecoveryLocked = false;

        wasActiveEquipmentButtonHeld = activeButtonHeld;
    }

    float GetCurrentMoveSpeed()
    {
        ItemData sprintBoots = GetSprintBootsItem();
        if (sprintBoots == null || !IsSprintActive())
            return moveSpeed;

        return moveSpeed * Mathf.Max(1f, sprintBoots.sprintSpeedMultiplier);
    }

    bool IsSprintActive()
    {
        return IsActiveEquipmentButtonHeld() &&
               !sprintRecoveryLocked &&
               moveInput.sqrMagnitude > 0.0001f &&
               currentStamina > 0f &&
               GetSprintBootsItem() != null;
    }

    bool IsActiveEquipmentButtonHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.isPressed)
            return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.isPressed;
    }

    ItemData GetSprintBootsItem()
    {
        EquipmentInventory equipment = inventory != null ? inventory.Equipment : null;
        ItemData activeItem = equipment != null ? equipment.GetEquippedItem(EquipmentSlotType.ActiveSlot) : null;
        if (activeItem == null || activeItem.activeEquipmentEffect != ActiveEquipmentEffectType.SprintBoots)
            return null;

        return activeItem;
    }

    bool TryStartDash(ItemData item)
    {
        if (item == null || isDashing || dashCooldownRemaining > 0f)
            return false;

        Vector2 direction = moveInput.sqrMagnitude > 0.0001f ? moveInput : lastMoveDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = new Vector2(facingX, 0f);

        dashCooldownRemaining = Mathf.Max(0f, item.cooldown);
        dashCooldownItem = item;
        StartCoroutine(DashRoutine(direction.normalized, item));
        return true;
    }

    IEnumerator DashRoutine(Vector2 direction, ItemData item)
    {
        isDashing = true;
        SpawnDashGhost();

        Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 target = start + direction * Mathf.Max(0f, item.dashDistance);
        float duration = Mathf.Max(0.02f, item.dashDuration);
        float elapsed = 0f;
        float nextGhostTime = 0.04f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector2 nextPosition = Vector2.Lerp(start, target, t);

            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            if (elapsed >= nextGhostTime)
            {
                SpawnDashGhost();
                nextGhostTime += 0.04f;
            }

            yield return null;
        }

        isDashing = false;
    }

    bool TryUseMedkit(ItemData item)
    {
        if (item == null || medkitCooldownRemaining > 0f || playerHealth == null)
            return false;

        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
            return false;

        playerHealth.Heal(Mathf.Max(1, item.healAmount));
        medkitCooldownRemaining = Mathf.Max(0f, item.cooldown);
        medkitCooldownItem = item;
        StartCoroutine(FlashPlayerVisual(medkitFlashColor, 0.18f));
        AudioController.Instance?.PlayPotion();
        return true;
    }

    bool TryUseShield(ItemData item)
    {
        if (item == null || shieldCooldownRemaining > 0f || shieldRemaining > 0f || playerHealth == null)
            return false;

        float duration = Mathf.Max(0.1f, item.effectDuration);
        shieldRemaining = duration;
        shieldCooldownRemaining = Mathf.Max(0f, item.cooldown);
        activeShieldItem = item;
        shieldCooldownItem = item;
        playerHealth.ApplyIncomingDamageMultiplier(item.shieldDamageMultiplier, duration);
        SetShieldVisualVisible(true);
        return true;
    }

    void SpawnDashGhost()
    {
        SpriteRenderer source = GetPlayerVisualRenderer();
        if (source == null || source.sprite == null)
            return;

        GameObject ghost = new GameObject("DashGhost");
        ghost.transform.position = source.transform.position;
        ghost.transform.rotation = source.transform.rotation;
        ghost.transform.localScale = source.transform.lossyScale;

        SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
        renderer.color = dashGhostColor;

        StartCoroutine(FadeAndDestroy(ghost, renderer, 0.22f));
    }

    IEnumerator FadeAndDestroy(GameObject target, SpriteRenderer renderer, float duration)
    {
        Color startColor = renderer != null ? renderer.color : Color.white;
        float elapsed = 0f;

        while (elapsed < duration && target != null && renderer != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            renderer.color = color;
            yield return null;
        }

        if (target != null)
            Destroy(target);
    }

    IEnumerator FlashPlayerVisual(Color flashColor, float duration)
    {
        SpriteRenderer renderer = GetPlayerVisualRenderer();
        if (renderer == null)
            yield break;

        Color original = renderer.color;
        renderer.color = Color.Lerp(original, flashColor, flashColor.a);
        yield return new WaitForSeconds(duration);

        if (renderer != null)
            renderer.color = original;
    }

    SpriteRenderer GetPlayerVisualRenderer()
    {
        if (cachedVisualRenderer != null)
            return cachedVisualRenderer;

        if (animator != null)
            cachedVisualRenderer = animator.GetComponentInChildren<SpriteRenderer>();

        if (cachedVisualRenderer == null)
            cachedVisualRenderer = GetComponentInChildren<SpriteRenderer>();

        return cachedVisualRenderer;
    }

    void SetShieldVisualVisible(bool visible)
    {
        if (!visible)
        {
            if (shieldVisual != null)
                shieldVisual.SetActive(false);
            return;
        }

        EnsureShieldVisual();
        if (shieldVisual != null)
            shieldVisual.SetActive(true);
    }

    void EnsureShieldVisual()
    {
        if (shieldVisual != null)
            return;

        shieldVisual = new GameObject("ActiveShieldVisual");
        shieldVisual.transform.SetParent(shieldVisualSpawnPoint != null ? shieldVisualSpawnPoint : transform, false);
        shieldVisual.transform.localPosition = Vector3.zero;

        LineRenderer line = shieldVisual.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 40;
        line.widthMultiplier = 0.04f;
        line.sortingOrder = 20;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = shieldColor;
        line.endColor = shieldColor;

        float radius = 0.72f;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
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

        float total = Mathf.Max(0.05f, dungeonTransitionSeconds);
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
            AudioController.Instance?.PlayToolSwing();
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
        AudioController.Instance?.PlayToolSwing();
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
                    AudioController.Instance?.PlayInteract();
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

        Vector3 attackOrigin = GetAttackOrigin();
        Transform swingPivot = attackSpawnPoint != null ? attackSpawnPoint : transform;
        GameObject attack = Instantiate(item.attackPrefab, attackOrigin, Quaternion.identity, transform);
        AttackHitbox hitbox = attack.GetComponent<AttackHitbox>();
        if (hitbox == null)
            return false;

        float swingRadius = weaponSwingRadius;

        hitbox.SetDamage(0);
        hitbox.SetOwner(playerHealth);
        hitbox.ConfigureSwing(
            attackDirection,
            swingPivot,
            swingRadius,
            weaponSwingArc * 0.5f,
            weaponSwingDuration
        );
        PlayAttackPunch();
        return true;
    }

    void PlayAttackPunch()
    {
        SpriteRenderer renderer = GetPlayerVisualRenderer();
        if (renderer == null)
            return;

        attackPunchTween?.Kill();
        attackPunchTween = renderer.transform
            .DOPunchScale(attackPunchScale, attackPunchDuration, 4, 0.5f)
            .SetEase(Ease.OutQuad);
    }

    Vector3 GetAttackOrigin()
    {
        return attackSpawnPoint != null ? attackSpawnPoint.position : transform.position;
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
            return FailPlacement();

        switch (item.placementMode)
        {
            case PlacementMode.Prefab:
                return PlaceStructurePrefab(item, anchorCell, consumeFromCursor);

            case PlacementMode.Tile:
                return PlaceStructureTile(item, anchorCell, consumeFromCursor);
        }

        return FailPlacement();
    }
    
    bool TryGetPlacementCell(ItemData item, out Vector3Int anchorCell)
    {
        anchorCell = default;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        if (Vector2.Distance(transform.position, mouseWorld) > item.actionRadius)
        {
            placementPreview?.PlayInvalidFeedback();
            return false;
        }

        EnsurePlacementTilemaps();
        if (groundTilemap == null)
        {
            placementPreview?.PlayInvalidFeedback();
            return false;
        }

        anchorCell = groundTilemap.WorldToCell(mouseWorld);
        return true;
    }

    bool PlaceStructurePrefab(ItemData item, Vector3Int anchorCell, bool consumeFromCursor)
    {
        if (item.prefab == null)
            return FailPlacement();

        if (item.isDoor)
            return PlaceDoor(item, anchorCell, consumeFromCursor);

        int rotationSteps = placementPreview != null ? placementPreview.CurrentRotationSteps : 0;
        BuildPlacementCells(item.prefab, anchorCell, rotationSteps);
        if (!WorldGrid.CanPlaceObject(placementCells))
            return FailPlacement();

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return FailPlacement();

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
            return FailPlacement();

        if (!DoorPlacementUtility.CanPlaceDoor(targetTilemap, buildTilemap, decorTilemap, anchorCell, item.doorWallMarkerTile, out Vector3Int firstDirection, out Vector3Int secondDirection))
            return FailPlacement();

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return FailPlacement();

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

        saveable.Initialize(item, IsHorizontalDoor(firstDirection, secondDirection) ? 0 : 1);

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
            return FailPlacement();

        Tilemap targetTilemap = item.occupiesBuildCell ? buildTilemap : decorTilemap;
        if (targetTilemap == null)
            return FailPlacement();

        // Prevent stacking multiple decor/build tiles into the same cell.
        if (targetTilemap.HasTile(anchorCell))
            return FailPlacement();

        if (!WorldGrid.CanPlaceBuildTile(anchorCell))
            return FailPlacement();

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return FailPlacement();

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
            return FailPlacement();

        if (!WorldGrid.CanPlaceBridgeTile(anchorCell))
            return FailPlacement();

        if (!TryConsumePlacedItem(item, consumeFromCursor))
            return FailPlacement();

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

    bool FailPlacement()
    {
        placementPreview?.PlayInvalidFeedback();
        return false;
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
            if (tm.gameObject.layer == decorLayer || tm.gameObject.name == "Decor" || tm.gameObject.name == "DecorTilemap")
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
                inventoryUI,
                buildTilemap,
                decorTilemap
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
                inventoryUI,
                buildTilemap,
                decorTilemap
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
