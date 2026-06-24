using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Tool Requirements")]
    public ToolType requiredTool = ToolType.None;
    public int minToolPower = 1;

    private ObjectHealthUI healthUI;

    public int CurrentHealth => currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
        healthUI = GetComponentInChildren<ObjectHealthUI>();

        if (healthUI != null)
            healthUI.SetHealth(currentHealth, maxHealth);
    }

    public void TryDamage(ItemData item)
    {
        if (item == null)
            return;

        if (item.type == ItemType.Tool)
        {
            if (!IsCorrectTool(item))
                return;

            TakeDamage(item.toolPower);
        }
        else if (item.type == ItemType.Weapon)
        {
            TakeDamage(item.damage);
        }
    }

    bool IsCorrectTool(ItemData item)
    {
        if (requiredTool == ToolType.None)
            return true;

        if (requiredTool == ToolType.Axe && item.toolType == ToolType.Axe)
            return item.toolPower >= minToolPower;

        if (requiredTool == ToolType.Pickaxe && item.toolType == ToolType.Pickaxe)
            return item.toolPower >= minToolPower;

        return false;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth -= damage;
        HitFlashFeedback.PlayOn(gameObject, new Color(1f, 0.95f, 0.75f, 1f), 0.1f);

        if (healthUI != null)
            healthUI.SetHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    public void RestoreHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, 1, maxHealth);

        if (healthUI != null)
            healthUI.SetHealth(currentHealth, maxHealth);
    }

    void Die()
    {
        ResourceDrop resourceDrop = GetComponent<ResourceDrop>();
        if (resourceDrop != null)
            resourceDrop.DropNow();

        Destroy(gameObject);
    }
}
