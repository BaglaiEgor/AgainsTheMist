using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerVitalsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerVitals playerVitals;

    [Header("HP UI")]
    [SerializeField] private Image hpRedFill;
    [SerializeField] private Image hpWhiteFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private float whiteCatchupSpeed = 0.6f;

    [Header("Food UI")]
    [SerializeField] private Image foodFill;
    [SerializeField] private TextMeshProUGUI foodText;

    private int lastHealth = -1;

    private void Awake()
    {
        whiteCatchupSpeed = Mathf.Max(0.01f, whiteCatchupSpeed);
        RefreshAll(true);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged += HandleHealthChanged;

        if (playerVitals != null)
            playerVitals.VitalsChanged += HandleVitalsChanged;

        RefreshAll(true);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= HandleHealthChanged;

        if (playerVitals != null)
            playerVitals.VitalsChanged -= HandleVitalsChanged;
    }

    private void Update()
    {
        if (hpWhiteFill == null || hpRedFill == null)
            return;

        if (hpWhiteFill.fillAmount <= hpRedFill.fillAmount)
            return;

        hpWhiteFill.fillAmount = Mathf.MoveTowards(
            hpWhiteFill.fillAmount,
            hpRedFill.fillAmount,
            whiteCatchupSpeed * Time.deltaTime
        );
    }

    private void HandleHealthChanged(int current, int max)
    {
        bool healed = lastHealth >= 0 && current >= lastHealth;
        UpdateHealthVisual(current, max, healed);
    }

    private void HandleVitalsChanged(int hpCurrent, int hpMax, int hungerCurrent, int hungerMax)
    {
        bool healed = lastHealth >= 0 && hpCurrent >= lastHealth;
        UpdateHealthVisual(hpCurrent, hpMax, healed);
        UpdateFoodVisual(hungerCurrent, hungerMax);
    }

    private void RefreshAll(bool forceInstantWhite)
    {
        int hpCurrent = playerHealth != null ? playerHealth.CurrentHealth : 0;
        int hpMax = playerHealth != null ? playerHealth.MaxHealth : 0;
        UpdateHealthVisual(hpCurrent, hpMax, forceInstantWhite);

        int hungerCurrent = playerVitals != null ? playerVitals.CurrentHungerDisplay : 0;
        int hungerMax = playerVitals != null ? playerVitals.MaxHunger : 0;
        UpdateFoodVisual(hungerCurrent, hungerMax);
    }

    private void UpdateHealthVisual(int current, int max, bool instantWhite)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        float normalized = (float)current / max;

        if (hpRedFill != null)
            hpRedFill.fillAmount = normalized;

        if (hpWhiteFill != null && (instantWhite || hpWhiteFill.fillAmount < normalized))
            hpWhiteFill.fillAmount = normalized;

        if (hpText != null)
            hpText.text = current + "/" + max;

        lastHealth = current;
    }

    private void UpdateFoodVisual(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (foodFill != null)
            foodFill.fillAmount = (float)current / max;

        if (foodText != null)
            foodText.text = current + "/" + max;
    }
}
