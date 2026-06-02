using System;
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

    private bool isDead;

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

        EnsureHealthText();
        RefreshHealthPresentation(false);
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isDead)
            return;

        int nextHealth = Mathf.Max(0, currentHealth - amount);
        if (nextHealth == currentHealth)
            return;

        currentHealth = nextHealth;
        RefreshHealthPresentation(true);

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            Debug.Log("Player died");
            Died?.Invoke();
        }
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
            return;
        }

        if (logIfNoUi)
            Debug.Log($"Player HP: {currentHealth}/{maxHealth}");
    }
}
