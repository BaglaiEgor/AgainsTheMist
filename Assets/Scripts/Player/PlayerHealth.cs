using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("UI (Optional)")]
    [SerializeField] private bool autoCreateHealthText = true;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private string healthTextFormat = "Здоровье: {0}/{1}";
    [SerializeField] private Vector3 healthTextPunchScale = new Vector3(0.08f, 0.08f, 0f);

    [Header("Refs")]
    [SerializeField] private Inventory inventory;

    private bool isDead;
    private Tween healthTextTween;
    private Vector3 healthTextBaseScale = Vector3.one;
    private float incomingDamageMultiplier = 1f;
    private float incomingDamageMultiplierTime;

    public event Action<int, int> HealthChanged;
    public event Action Died;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        if (currentHealth <= 0 || currentHealth > maxHealth)
            currentHealth = maxHealth;

        ResolveReferences();

        if (GetComponent<PlayerDeathRecovery>() == null)
            gameObject.AddComponent<PlayerDeathRecovery>();

        EnsureHealthText();
        if (healthText != null)
            healthTextBaseScale = healthText.transform.localScale;
        RefreshHealthPresentation(false);
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    void Update()
    {
        if (incomingDamageMultiplierTime <= 0f)
            return;

        incomingDamageMultiplierTime = Mathf.Max(0f, incomingDamageMultiplierTime - Time.deltaTime);
        if (incomingDamageMultiplierTime <= 0f)
            incomingDamageMultiplier = 1f;
    }

    public void TakeDamage(int amount)
    {
        TakeDamageInternal(amount, true);
    }

    public void TakeFogDamage(int amount)
    {
        TakeDamageInternal(amount, false);
    }

    private void TakeDamageInternal(int amount, bool applyArmor)
    {
        if (amount <= 0 || isDead)
            return;

        int finalAmount = Mathf.CeilToInt(amount * Mathf.Clamp01(incomingDamageMultiplier));
        if (applyArmor)
            finalAmount = Mathf.Max(1, finalAmount - GetArmorDefense());

        if (finalAmount <= 0)
            return;

        int nextHealth = Mathf.Max(0, currentHealth - finalAmount);
        if (nextHealth == currentHealth)
            return;

        currentHealth = nextHealth;
        RefreshHealthPresentation(true);

        if (currentHealth > 0)
            CameraShakeController.ShakeMain(0.1f, 0.055f);

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            Died?.Invoke();
        }
    }

    public void Revive()
    {
        isDead = false;
        currentHealth = maxHealth;
        RefreshHealthPresentation(true);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || isDead)
            return;

        int nextHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (nextHealth == currentHealth)
            return;

        currentHealth = nextHealth;
        RefreshHealthPresentation(true);
    }

    public void Restore(int health, int max)
    {
        maxHealth = Mathf.Max(1, max);
        currentHealth = Mathf.Clamp(health <= 0 ? maxHealth : health, 0, maxHealth);
        isDead = currentHealth <= 0;
        RefreshHealthPresentation(true);
    }

    public void ApplyIncomingDamageMultiplier(float multiplier, float duration)
    {
        incomingDamageMultiplier = Mathf.Clamp01(multiplier);
        incomingDamageMultiplierTime = Mathf.Max(0f, duration);
        if (incomingDamageMultiplierTime <= 0f)
            incomingDamageMultiplier = 1f;
    }

    private int GetArmorDefense()
    {
        if (inventory == null)
            ResolveReferences();

        EquipmentInventory equipment = inventory != null ? inventory.Equipment : null;
        return equipment != null ? equipment.GetTotalDefense() : 0;
    }

    private void ResolveReferences()
    {
        if (inventory != null)
            return;

        inventory = GetComponent<Inventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();
    }

    void EnsureHealthText()
    {
        if (healthText != null || !autoCreateHealthText)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("PlayerHealthText");
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            healthText = existingText;
            return;
        }

        GameObject textObject = new GameObject("PlayerHealthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(24f, -24f);
        rectTransform.sizeDelta = new Vector2(260f, 48f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.raycastTarget = false;

        healthText = text;
    }

    void RefreshHealthPresentation(bool logIfNoUi)
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (healthText != null)
        {
            healthText.text = string.Format(healthTextFormat, currentHealth, maxHealth);
            if (logIfNoUi)
                PlayHealthTextFeedback();
            return;
        }

        if (logIfNoUi)
            Debug.Log($"Player HP: {currentHealth}/{maxHealth}");
    }

    void PlayHealthTextFeedback()
    {
        if (healthText == null)
            return;

        healthTextTween?.Kill();
        healthText.transform.localScale = healthTextBaseScale;
        healthTextTween = healthText.transform
            .DOPunchScale(healthTextPunchScale, 0.14f, 6, 0.45f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => healthText.transform.localScale = healthTextBaseScale);
    }

    void OnDisable()
    {
        healthTextTween?.Kill();
        if (healthText != null)
            healthText.transform.localScale = healthTextBaseScale;
    }
}
