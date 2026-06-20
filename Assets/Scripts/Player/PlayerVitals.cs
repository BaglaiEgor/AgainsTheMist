using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerVitals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Inventory inventory;

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100;
    [SerializeField] private float currentHunger = 100f;
    [SerializeField] private float hungerDrainPerSecond = 0.25f;

    [Header("Health Tick")]
    [SerializeField] private float healthTickInterval = 2f;
    [SerializeField] private int healPerTick = 1;
    [SerializeField] private int starvationDamagePerTick = 1;

    private float healthTickTimer;

    public event Action<int, int, int, int> VitalsChanged;

    public float CurrentHunger => currentHunger;
    public int CurrentHungerDisplay => Mathf.Clamp(Mathf.CeilToInt(currentHunger), 0, maxHunger);
    public int MaxHunger => maxHunger;
    public float NormalizedHunger => maxHunger <= 0 ? 0f : Mathf.Clamp01(currentHunger / maxHunger);

    private void Awake()
    {
        maxHunger = Mathf.Max(1, maxHunger);
        currentHunger = Mathf.Clamp(currentHunger <= 0f ? maxHunger : currentHunger, 0f, maxHunger);
        healthTickInterval = Mathf.Max(0.01f, healthTickInterval);
        healPerTick = Mathf.Max(1, healPerTick);
        starvationDamagePerTick = Mathf.Max(1, starvationDamagePerTick);

        if (playerHealth != null)
            playerHealth.HealthChanged += HandleHealthChanged;

        RaiseVitalsChanged();
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= HandleHealthChanged;
    }

    private void OnValidate()
    {
        maxHunger = Mathf.Max(1, maxHunger);
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
        hungerDrainPerSecond = Mathf.Max(0f, hungerDrainPerSecond);
        healthTickInterval = Mathf.Max(0.01f, healthTickInterval);
        healPerTick = Mathf.Max(1, healPerTick);
        starvationDamagePerTick = Mathf.Max(1, starvationDamagePerTick);
    }

    private void Update()
    {
        if (playerHealth == null || playerHealth.IsDead)
            return;

        float previousHunger = currentHunger;
        currentHunger = Mathf.Max(0f, currentHunger - hungerDrainPerSecond * Time.deltaTime);

        bool hungerChanged = !Mathf.Approximately(previousHunger, currentHunger);
        ApplyHealthTick();

        if (hungerChanged)
            RaiseVitalsChanged();
    }

    public bool TryEat(ItemData item)
    {
        if (item == null || item.type != ItemType.Food)
            return false;
        if (item.foodRestore <= 0)
            return false;
        if (inventory == null)
            return false;

        if (!InventoryTransactionService.TryConsume(inventory, item, 1))
            return false;

        return TryApplyFood(item);
    }

    public bool TryApplyFood(ItemData item)
    {
        if (item == null || item.type != ItemType.Food)
            return false;
        if (item.foodRestore <= 0)
            return false;

        currentHunger = Mathf.Clamp(currentHunger + item.foodRestore, 0f, maxHunger);
        healthTickTimer = 0f;
        RaiseVitalsChanged();
        return true;
    }

    public void RestoreHunger(float hunger)
    {
        currentHunger = Mathf.Clamp(hunger, 0f, maxHunger);
        healthTickTimer = 0f;
        RaiseVitalsChanged();
    }

    public void RestoreFull()
    {
        currentHunger = maxHunger;
        healthTickTimer = 0f;
        RaiseVitalsChanged();
    }

    private void ApplyHealthTick()
    {
        if (playerHealth == null || playerHealth.IsDead)
            return;

        healthTickTimer += Time.deltaTime;
        if (healthTickTimer < healthTickInterval)
            return;

        while (healthTickTimer >= healthTickInterval)
        {
            healthTickTimer -= healthTickInterval;

            if (currentHunger > 0f)
            {
                if (playerHealth.CurrentHealth < playerHealth.MaxHealth)
                    playerHealth.Heal(healPerTick);
            }
            else
            {
                playerHealth.TakeDamage(starvationDamagePerTick);
                if (playerHealth.IsDead)
                {
                    healthTickTimer = 0f;
                    break;
                }
            }
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        RaiseVitalsChanged();
    }

    private void RaiseVitalsChanged()
    {
        int hpCurrent = playerHealth != null ? playerHealth.CurrentHealth : 0;
        int hpMax = playerHealth != null ? playerHealth.MaxHealth : 0;
        VitalsChanged?.Invoke(hpCurrent, hpMax, CurrentHungerDisplay, maxHunger);
    }
}
