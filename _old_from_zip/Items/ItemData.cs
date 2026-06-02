using UnityEngine;
using UnityEngine.Tilemaps;

public enum ConsumableEffectType
{
    None,
    Heal,
    FogPressureProtection,
    FrostProtection
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Farm/Item")]
public class ItemData : ScriptableObject
{    
    public string itemID;
    public string itemName;
    public string description;
    public Sprite icon;
    public int maxStack = 99;

    public ItemType type;
    public PlacementMode placementMode;

    [Header("Structure Prefab (for Structure + Prefab placement mode)")]
    public GameObject prefab;
    
    [Header("Structure Tile (for Structure + Tile placement mode)")]
    public TileBase tileToPlace;
    [Tooltip("If true, tile is placed to Build tilemap and blocks build placement. If false, goes to Decor tilemap and does not block placement.")]
    public bool occupiesBuildCell = true;

    [Header("Seed")]
    public CropDefinition seedCrop;

    [Header("Tool / Weapon")]
    public GameObject attackPrefab;
    public float actionRadius = 3f;
    public ToolType toolType;
    public int toolPower = 1;
    public int damage = 1;

    [Header("Lantern")]
    public bool isLantern;
    public float lanternLightRadius = 2.5f;
    public float lanternDrainPerSecond = 1f;
    public float lanternMaxCharge = 100f;

    [Header("Consumable (ItemType.Consumable)")]
    public ConsumableEffectType consumableEffectType = ConsumableEffectType.None;
    [Min(0f)] public float effectDuration = 0f;
    [Min(0)] public int healAmount = 0;
    [Min(0f)] public float cooldown = 0f;

    [Header("Equipment Protection (ItemType.Equipment)")]
    [Min(0f)] public float frostDamageMultiplier = 1f;
    [Min(0f)] public float pressureGrowthMultiplier = 1f;

    [Header("Furnace")]
    [Tooltip("How many seconds this item burns in furnace fuel slot. 0 = not a fuel.")]
    [Min(0f)] public float furnaceFuelSeconds = 0f;
    [Tooltip("Item produced when this item is smelted in furnace input slot.")]
    public ItemData furnaceSmeltResult;
    [Tooltip("How many items are produced per 1 smelted input item.")]
    [Min(1)] public int furnaceSmeltResultAmount = 1;
    [Tooltip("Seconds required to smelt one input item. If 0, furnace default is used.")]
    [Min(0f)] public float furnaceSmeltDuration = 0f;
}

