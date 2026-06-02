using UnityEngine;

[DisallowMultipleComponent]
public class PlayerPotionEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Active Effects")]
    [SerializeField] private float fogProtectionRemainingTime;
    [SerializeField] private float frostProtectionRemainingTime;
    [SerializeField] private float healCooldownRemainingTime;

    [SerializeField] private float activePressureGrowthMultiplier = 1f;
    [SerializeField] private float activeFrostDamageMultiplier = 1f;

    [SerializeField] private ItemData activeFogProtectionPotion;
    [SerializeField] private ItemData activeFrostProtectionPotion;
    [SerializeField] private ItemData activeHealingCooldownPotion;

    public float RemainingFogEffectTime => fogProtectionRemainingTime;
    public float RemainingFrostEffectTime => frostProtectionRemainingTime;
    public float RemainingHealCooldown => healCooldownRemainingTime;

    public ItemData ActiveFogProtectionPotion => fogProtectionRemainingTime > 0f ? activeFogProtectionPotion : null;
    public ItemData ActiveFrostProtectionPotion => frostProtectionRemainingTime > 0f ? activeFrostProtectionPotion : null;
    public ItemData ActiveHealingCooldownPotion => healCooldownRemainingTime > 0f ? activeHealingCooldownPotion : null;

    public string ActiveFogProtectionPotionName => ActiveFogProtectionPotion != null ? ActiveFogProtectionPotion.itemName : string.Empty;
    public string ActiveFrostProtectionPotionName => ActiveFrostProtectionPotion != null ? ActiveFrostProtectionPotion.itemName : string.Empty;
    public string ActiveHealingCooldownPotionName => ActiveHealingCooldownPotion != null ? ActiveHealingCooldownPotion.itemName : string.Empty;

    public Sprite ActiveFogProtectionPotionIcon => ActiveFogProtectionPotion != null ? ActiveFogProtectionPotion.icon : null;
    public Sprite ActiveFrostProtectionPotionIcon => ActiveFrostProtectionPotion != null ? ActiveFrostProtectionPotion.icon : null;
    public Sprite ActiveHealingCooldownPotionIcon => ActiveHealingCooldownPotion != null ? ActiveHealingCooldownPotion.icon : null;

    void Awake()
    {
        EnsurePlayerHealthReference();
    }

    void Update()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f)
            return;

        if (fogProtectionRemainingTime > 0f)
        {
            fogProtectionRemainingTime = Mathf.Max(0f, fogProtectionRemainingTime - delta);
            if (fogProtectionRemainingTime <= 0f)
            {
                activePressureGrowthMultiplier = 1f;
                activeFogProtectionPotion = null;
            }
        }

        if (frostProtectionRemainingTime > 0f)
        {
            frostProtectionRemainingTime = Mathf.Max(0f, frostProtectionRemainingTime - delta);
            if (frostProtectionRemainingTime <= 0f)
            {
                activeFrostDamageMultiplier = 1f;
                activeFrostProtectionPotion = null;
            }
        }

        if (healCooldownRemainingTime > 0f)
        {
            healCooldownRemainingTime = Mathf.Max(0f, healCooldownRemainingTime - delta);
            if (healCooldownRemainingTime <= 0f)
                activeHealingCooldownPotion = null;
        }
    }

    public bool TryUsePotion(ItemData potion)
    {
        if (potion == null)
            return false;

        if (potion.type != ItemType.Consumable)
            return false;

        switch (potion.consumableEffectType)
        {
            case ConsumableEffectType.Heal:
                return TryUseHealingPotion(potion);
            case ConsumableEffectType.FogPressureProtection:
                ApplyFogProtection(potion);
                return true;
            case ConsumableEffectType.FrostProtection:
                ApplyFrostProtection(potion);
                return true;
            default:
                return false;
        }
    }

    public float GetPressureGrowthMultiplier()
    {
        if (fogProtectionRemainingTime <= 0f)
            return 1f;

        return Mathf.Max(0f, activePressureGrowthMultiplier);
    }

    public float GetFrostDamageMultiplier()
    {
        if (frostProtectionRemainingTime <= 0f)
            return 1f;

        return Mathf.Max(0f, activeFrostDamageMultiplier);
    }

    bool TryUseHealingPotion(ItemData potion)
    {
        if (healCooldownRemainingTime > 0f)
            return false;

        EnsurePlayerHealthReference();
        if (playerHealth == null)
            return false;

        playerHealth.Heal(Mathf.Max(0, potion.healAmount));
        healCooldownRemainingTime = Mathf.Max(0f, potion.cooldown);
        activeHealingCooldownPotion = potion;
        return true;
    }

    void ApplyFogProtection(ItemData potion)
    {
        fogProtectionRemainingTime = Mathf.Max(0f, potion.effectDuration);
        activePressureGrowthMultiplier = Mathf.Max(0f, potion.pressureGrowthMultiplier);
        activeFogProtectionPotion = potion;
    }

    void ApplyFrostProtection(ItemData potion)
    {
        frostProtectionRemainingTime = Mathf.Max(0f, potion.effectDuration);
        activeFrostDamageMultiplier = Mathf.Max(0f, potion.frostDamageMultiplier);
        activeFrostProtectionPotion = potion;
    }

    void EnsurePlayerHealthReference()
    {
        if (playerHealth != null)
            return;

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInChildren<PlayerHealth>();
    }
}
