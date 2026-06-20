using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerVitalsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerVitals playerVitals;
    [SerializeField] private PlayerController playerController;

    [Header("HP UI")]
    [SerializeField] private Image hpRedFill;
    [SerializeField] private Image hpWhiteFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private float whiteCatchupSpeed = 0.6f;

    [Header("Food UI")]
    [SerializeField] private Image foodFill;
    [SerializeField] private TextMeshProUGUI foodText;

    [Header("Stamina UI")]
    [SerializeField] private GameObject staminaRoot;
    [SerializeField] private Image staminaFill;
    [SerializeField] private TextMeshProUGUI staminaText;

    private int lastHealth = -1;

    private void Awake()
    {
        whiteCatchupSpeed = Mathf.Max(0.01f, whiteCatchupSpeed);
        EnsureReferences();
        EnsureStaminaUi();
        RefreshAll(true);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged += HandleHealthChanged;

        if (playerVitals != null)
            playerVitals.VitalsChanged += HandleVitalsChanged;

        EnsureReferences();
        EnsureStaminaUi();
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
        UpdateStaminaVisual();

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

    private void EnsureReferences()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
    }

    private void EnsureStaminaUi()
    {
        if (staminaRoot != null || foodFill == null)
            return;

        Transform parent = foodFill.transform.parent != null ? foodFill.transform.parent : transform;

        staminaRoot = new GameObject("Stamina", typeof(RectTransform));
        staminaRoot.transform.SetParent(parent, false);

        RectTransform rootRect = staminaRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -28f);
        rootRect.sizeDelta = new Vector2(0f, 18f);

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(staminaRoot.transform, false);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.1f, 0.8f);

        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);
        staminaFill = fillObject.GetComponent<Image>();
        staminaFill.color = new Color(0.2f, 0.85f, 1f, 0.95f);
        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;

        RectTransform fillRect = staminaFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(staminaRoot.transform, false);
        staminaText = textObject.GetComponent<TextMeshProUGUI>();
        staminaText.fontSize = 12f;
        staminaText.alignment = TextAlignmentOptions.Center;
        staminaText.color = Color.white;
        staminaText.raycastTarget = false;

        RectTransform textRect = staminaText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void UpdateStaminaVisual()
    {
        EnsureReferences();

        bool visible = playerController != null && playerController.HasSprintBootsEquipped;
        if (staminaRoot != null && staminaRoot.activeSelf != visible)
            staminaRoot.SetActive(visible);

        if (!visible)
            return;

        float max = Mathf.Max(1f, playerController.MaxStamina);
        float current = Mathf.Clamp(playerController.CurrentStamina, 0f, max);

        if (staminaFill != null)
            staminaFill.fillAmount = current / max;

        if (staminaText != null)
            staminaText.text = Mathf.CeilToInt(current) + "/" + Mathf.CeilToInt(max);
    }
}
