using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFogDamage : MonoBehaviour
{
    [Header("Fog Damage")]
    [SerializeField] private float damageInterval = 1.5f;
    [SerializeField] private int damageAmount = 1;

    [Header("Refs")]
    [SerializeField] private PlayerHealth playerHealth;

    private float damageTimer;

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (playerHealth == null || playerHealth.IsDead)
            return;

        if (damageAmount <= 0)
            return;

        float interval = Mathf.Max(0.01f, damageInterval);
        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        bool inFog = fogSystem.IsPositionInFog(transform.position);
        if (!inFog)
        {
            damageTimer = 0f;
            return;
        }

        damageTimer += Time.deltaTime;
        if (damageTimer < interval)
            return;

        damageTimer = 0f;
        playerHealth.TakeFogDamage(damageAmount);
    }
}
