using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PlayerStatsTooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private ItemTooltip tooltip;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Inventory inventory;

    [Header("Text")]
    [SerializeField] private string header = "Характеристики";

    private bool hovered;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!hovered)
            return;

        ShowStatsTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ShowStatsTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (tooltip != null)
            tooltip.Hide();
    }

    private void ResolveReferences()
    {
        if (tooltip == null)
            tooltip = FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include);

        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();
    }

    private void ShowStatsTooltip()
    {
        if (tooltip == null)
            return;

        ResolveReferences();

        int hpCurrent = playerHealth != null ? playerHealth.CurrentHealth : 0;
        int hpMax = playerHealth != null ? playerHealth.MaxHealth : 0;
        int defense = 0;

        float pressureMultiplier = 1f;
        float frostMultiplier = 1f;

        if (inventory != null)
        {
            pressureMultiplier = inventory.GetCombinedPressureGrowthMultiplier();
            frostMultiplier = inventory.GetCombinedFrostDamageMultiplier();

            EquipmentInventory equipment = inventory.Equipment;
            if (equipment != null)
                defense = equipment.GetTotalDefense();
        }

        string extra =
            "хп: " + hpCurrent + "/" + hpMax + "\n" +
            "защита: " + defense + "\n" +
            "сопротивление туману: " + FormatResistancePercent(pressureMultiplier) + "\n" +
            "сопротивление морозному туману: " + FormatResistancePercent(frostMultiplier);

        tooltip.ShowCustom(header, string.Empty, extra);
    }

    private static string FormatResistancePercent(float damageOrGrowthMultiplier)
    {
        float resistance = (1f - damageOrGrowthMultiplier) * 100f;
        return resistance.ToString("0.#") + "%";
    }
}
