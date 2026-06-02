using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FrostFogZone : MonoBehaviour
{
    private static readonly HashSet<FrostFogZone> activeZones = new HashSet<FrostFogZone>();

    [Header("Frost Effect")]
    [Min(1)] [SerializeField] private int damageAmount = 2;
    [Min(0.05f)] [SerializeField] private float damageInterval = 1.5f;
    [SerializeField] private bool logFrostHits;

    [Header("Visual")]
    [SerializeField] private ParticleSystem[] frostParticles;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool usePointCheckFallback = true;

    [Header("Particle Style")]
    [SerializeField] private bool autoConfigureParticles = true;
    [Min(0.1f)] [SerializeField] private float baseEmissionRate = 14f;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(2.2f, 4.5f);
    [SerializeField] private Vector2 sizeRange = new Vector2(0.08f, 0.16f);
    [SerializeField] private Color particleTint = new Color(0.88f, 0.95f, 1f, 0.7f);
    [SerializeField] private Vector2 windDirection = new Vector2(-1f, -1f);
    [Min(0f)] [SerializeField] private float windSpeed = 0.7f;

    private readonly HashSet<Transform> playersInside = new HashSet<Transform>();
    private readonly List<Transform> cleanupBuffer = new List<Transform>();
    private readonly Dictionary<Transform, PlayerHealth> playerHealthByTransform = new Dictionary<Transform, PlayerHealth>();
    private readonly Dictionary<Transform, Inventory> playerInventoryByTransform = new Dictionary<Transform, Inventory>();
    private readonly Dictionary<Transform, PlayerPotionEffects> playerPotionEffectsByTransform = new Dictionary<Transform, PlayerPotionEffects>();
    private readonly Dictionary<Transform, float> nextDamageTimeByPlayer = new Dictionary<Transform, float>();
    private readonly Dictionary<ParticleSystem, float> baseEmissionByParticle = new Dictionary<ParticleSystem, float>();
    private Collider2D zoneCollider;
    private Transform fallbackPlayer;
    private float lastAppliedEmissionMultiplier = -1f;
    private bool visualsArePlaying;

    void Awake()
    {
        EnsureTriggerCollider();
        ResolveVisualReferences();
        ApplyVisualStyle();
        SetParticlesPlaying(false, true);
        ApplyParticles(0f, true);
    }

    void OnEnable()
    {
        activeZones.Add(this);
        ResolveVisualReferences();
        ApplyVisualStyle();
        SetParticlesPlaying(false, true);
        ApplyParticles(0f, true);
    }

    void OnDisable()
    {
        activeZones.Remove(this);
        playersInside.Clear();
        cleanupBuffer.Clear();
        playerHealthByTransform.Clear();
        playerInventoryByTransform.Clear();
        playerPotionEffectsByTransform.Clear();
        nextDamageTimeByPlayer.Clear();
        SetParticlesPlaying(false, true);
        ApplyParticles(0f, true);
    }

    void OnDestroy()
    {
        activeZones.Remove(this);
    }

    void Update()
    {
        SyncPlayerPresenceWithFallbackCheck();

        cleanupBuffer.Clear();
        foreach (Transform playerTransform in playersInside)
        {
            if (playerTransform == null)
            {
                cleanupBuffer.Add(playerTransform);
            }
        }

        for (int i = 0; i < cleanupBuffer.Count; i++)
        {
            playersInside.Remove(cleanupBuffer[i]);
            playerHealthByTransform.Remove(cleanupBuffer[i]);
            playerInventoryByTransform.Remove(cleanupBuffer[i]);
            playerPotionEffectsByTransform.Remove(cleanupBuffer[i]);
            nextDamageTimeByPlayer.Remove(cleanupBuffer[i]);
        }

        float targetEmissionMultiplier = ResolveTargetEmissionMultiplier();
        SetParticlesPlaying(targetEmissionMultiplier > 0.001f, false);
        ApplyParticles(targetEmissionMultiplier, false);

        ApplyFrostDamage();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerTransform(other, out Transform playerTransform))
            return;

        playersInside.Add(playerTransform);
        nextDamageTimeByPlayer[playerTransform] = Time.time + Mathf.Max(0.05f, damageInterval);
        if (TryGetPlayerHealth(playerTransform, out PlayerHealth playerHealth))
            playerHealthByTransform[playerTransform] = playerHealth;
        if (TryGetPlayerInventory(playerTransform, out Inventory playerInventory))
            playerInventoryByTransform[playerTransform] = playerInventory;
        if (TryGetPlayerPotionEffects(playerTransform, out PlayerPotionEffects potionEffects))
            playerPotionEffectsByTransform[playerTransform] = potionEffects;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayerTransform(other, out Transform playerTransform))
            return;

        playersInside.Remove(playerTransform);
        playerHealthByTransform.Remove(playerTransform);
        playerInventoryByTransform.Remove(playerTransform);
        playerPotionEffectsByTransform.Remove(playerTransform);
        nextDamageTimeByPlayer.Remove(playerTransform);
    }

    float ResolveTargetEmissionMultiplier()
    {
        if (playersInside.Count == 0)
            return 0f;

        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return 0f;

        foreach (Transform playerTransform in playersInside)
        {
            if (playerTransform == null)
                continue;

            if (fogSystem.IsPositionInFog(playerTransform.position))
                return 1f;
        }

        // Player inside frost zone, but in beacon safe-zone.
        return 0f;
    }

    void ApplyFrostDamage()
    {
        if (playersInside.Count == 0)
            return;

        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        float interval = Mathf.Max(0.05f, damageInterval);
        int damage = Mathf.Max(1, damageAmount);

        foreach (Transform playerTransform in playersInside)
        {
            if (playerTransform == null)
                continue;

            bool inFog = fogSystem.IsPositionInFog(playerTransform.position);
            if (!inFog)
            {
                nextDamageTimeByPlayer[playerTransform] = Time.time + interval;
                continue;
            }

            if (!TryGetPlayerHealth(playerTransform, out PlayerHealth playerHealth))
                continue;
            if (playerHealth == null || playerHealth.IsDead)
                continue;

            if (!nextDamageTimeByPlayer.TryGetValue(playerTransform, out float nextDamageTime))
                nextDamageTime = Time.time + interval;

            if (Time.time < nextDamageTime)
                continue;

            int appliedDamage = ResolveFrostDamageForPlayer(damage, playerTransform);
            if (appliedDamage > 0)
                playerHealth.TakeDamage(appliedDamage);

            nextDamageTimeByPlayer[playerTransform] = Time.time + interval;

            if (logFrostHits)
                Debug.Log($"FrostFogZone hit {playerTransform.name}: {appliedDamage} damage");
        }
    }

    bool TryGetPlayerTransform(Collider2D other, out Transform playerTransform)
    {
        playerTransform = null;
        if (other == null)
            return false;

        Transform candidate = other.transform;
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
            candidate = playerHealth.transform;
        else if (!string.IsNullOrWhiteSpace(playerTag))
        {
            Transform tagged = other.transform.root;
            if (tagged == null || !tagged.CompareTag(playerTag))
                return false;

            candidate = tagged;
        }

        playerTransform = candidate;
        return playerTransform != null;
    }

    bool TryGetPlayerHealth(Transform playerTransform, out PlayerHealth playerHealth)
    {
        playerHealth = null;
        if (playerTransform == null)
            return false;

        if (playerHealthByTransform.TryGetValue(playerTransform, out playerHealth) &&
            playerHealth != null)
        {
            return true;
        }

        playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = playerTransform.GetComponentInChildren<PlayerHealth>();

        if (playerHealth != null)
            playerHealthByTransform[playerTransform] = playerHealth;

        return playerHealth != null;
    }

    bool TryGetPlayerInventory(Transform playerTransform, out Inventory inventory)
    {
        inventory = null;
        if (playerTransform == null)
            return false;

        if (playerInventoryByTransform.TryGetValue(playerTransform, out inventory) &&
            inventory != null)
        {
            return true;
        }

        inventory = playerTransform.GetComponent<Inventory>();
        if (inventory == null)
            inventory = playerTransform.GetComponentInParent<Inventory>();
        if (inventory == null)
            inventory = playerTransform.GetComponentInChildren<Inventory>();

        if (inventory == null)
        {
            Inventory[] allInventories = Object.FindObjectsByType<Inventory>(FindObjectsSortMode.None);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < allInventories.Length; i++)
            {
                Inventory candidate = allInventories[i];
                if (candidate == null)
                    continue;

                float distance = Vector2.Distance(playerTransform.position, candidate.transform.position);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                inventory = candidate;
            }
        }

        if (inventory != null)
            playerInventoryByTransform[playerTransform] = inventory;

        return inventory != null;
    }

    int ResolveFrostDamageForPlayer(int baseDamage, Transform playerTransform)
    {
        int clampedBaseDamage = Mathf.Max(1, baseDamage);
        float multiplier = ResolveFrostDamageMultiplier(playerTransform);
        float scaledDamage = clampedBaseDamage * multiplier;

        if (scaledDamage <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.FloorToInt(scaledDamage));
    }

    float ResolveFrostDamageMultiplier(Transform playerTransform)
    {
        float equipmentMultiplier = 1f;
        if (TryGetPlayerInventory(playerTransform, out Inventory inventory) &&
            inventory != null)
        {
            ItemData equippedItem = inventory.EquippedItem;
            if (equippedItem != null && equippedItem.type == ItemType.Equipment)
                equipmentMultiplier = Mathf.Max(0f, equippedItem.frostDamageMultiplier);
        }

        float potionMultiplier = 1f;
        if (TryGetPlayerPotionEffects(playerTransform, out PlayerPotionEffects potionEffects) &&
            potionEffects != null)
        {
            potionMultiplier = Mathf.Max(0f, potionEffects.GetFrostDamageMultiplier());
        }

        return equipmentMultiplier * potionMultiplier;
    }

    bool TryGetPlayerPotionEffects(Transform playerTransform, out PlayerPotionEffects potionEffects)
    {
        potionEffects = null;
        if (playerTransform == null)
            return false;

        if (playerPotionEffectsByTransform.TryGetValue(playerTransform, out potionEffects) &&
            potionEffects != null)
        {
            return true;
        }

        potionEffects = playerTransform.GetComponent<PlayerPotionEffects>();
        if (potionEffects == null)
            potionEffects = playerTransform.GetComponentInParent<PlayerPotionEffects>();
        if (potionEffects == null)
            potionEffects = playerTransform.GetComponentInChildren<PlayerPotionEffects>();

        if (potionEffects != null)
            playerPotionEffectsByTransform[playerTransform] = potionEffects;

        return potionEffects != null;
    }

    void EnsureTriggerCollider()
    {
        zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    void SyncPlayerPresenceWithFallbackCheck()
    {
        if (!usePointCheckFallback)
            return;
        if (zoneCollider == null)
            EnsureTriggerCollider();
        if (zoneCollider == null)
            return;
        if (string.IsNullOrWhiteSpace(playerTag))
            return;

        if (fallbackPlayer == null || !fallbackPlayer.CompareTag(playerTag))
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            fallbackPlayer = playerObject != null ? playerObject.transform : null;
        }

        if (fallbackPlayer == null)
            return;

        bool inside = IsTransformInsideZone(fallbackPlayer);
        if (inside)
        {
            playersInside.Add(fallbackPlayer);
            if (!nextDamageTimeByPlayer.ContainsKey(fallbackPlayer))
                nextDamageTimeByPlayer[fallbackPlayer] = Time.time + Mathf.Max(0.05f, damageInterval);

            if (TryGetPlayerHealth(fallbackPlayer, out PlayerHealth playerHealth) && playerHealth != null)
                playerHealthByTransform[fallbackPlayer] = playerHealth;
            if (TryGetPlayerInventory(fallbackPlayer, out Inventory playerInventory) && playerInventory != null)
                playerInventoryByTransform[fallbackPlayer] = playerInventory;
            if (TryGetPlayerPotionEffects(fallbackPlayer, out PlayerPotionEffects potionEffects) && potionEffects != null)
                playerPotionEffectsByTransform[fallbackPlayer] = potionEffects;

            return;
        }

        playersInside.Remove(fallbackPlayer);
        playerHealthByTransform.Remove(fallbackPlayer);
        playerInventoryByTransform.Remove(fallbackPlayer);
        playerPotionEffectsByTransform.Remove(fallbackPlayer);
        nextDamageTimeByPlayer.Remove(fallbackPlayer);
    }

    bool IsTransformInsideZone(Transform target)
    {
        if (target == null)
            return false;
        if (zoneCollider == null)
            return false;

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();
        if (targetCollider == null)
            targetCollider = target.GetComponentInParent<Collider2D>();

        if (targetCollider != null && targetCollider.enabled)
        {
            Bounds zoneBounds = zoneCollider.bounds;
            zoneBounds.Expand(0.05f);
            return zoneBounds.Intersects(targetCollider.bounds);
        }

        Vector3 targetPosition = target.position;
        return zoneCollider.OverlapPoint(targetPosition) || zoneCollider.bounds.Contains(targetPosition);
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        if (zoneCollider == null)
            EnsureTriggerCollider();
        if (zoneCollider == null)
            return false;

        return zoneCollider.OverlapPoint(worldPosition) || zoneCollider.bounds.Contains(worldPosition);
    }

    public static bool IsAnyZoneActiveAtPosition(Vector3 worldPosition)
    {
        foreach (FrostFogZone zone in activeZones)
        {
            if (zone == null || !zone.isActiveAndEnabled || !zone.gameObject.activeInHierarchy)
                continue;

            if (zone.ContainsPoint(worldPosition))
                return true;
        }

        return false;
    }

    void ResolveVisualReferences()
    {
        if (frostParticles == null || frostParticles.Length == 0)
            frostParticles = GetComponentsInChildren<ParticleSystem>(true);
    }

    void ApplyParticles(float emissionMultiplier, bool force)
    {
        if (!force && Mathf.Approximately(lastAppliedEmissionMultiplier, emissionMultiplier))
            return;

        for (int i = 0; i < frostParticles.Length; i++)
        {
            ParticleSystem particle = frostParticles[i];
            if (particle == null)
                continue;

            if (!baseEmissionByParticle.TryGetValue(particle, out float baseEmission))
            {
                var emissionModule = particle.emission;
                baseEmission = Mathf.Max(0.01f, emissionModule.rateOverTimeMultiplier);
                baseEmissionByParticle[particle] = baseEmission;
            }

            var emission = particle.emission;
            emission.rateOverTimeMultiplier = baseEmission * Mathf.Clamp01(emissionMultiplier);
        }

        lastAppliedEmissionMultiplier = emissionMultiplier;
    }

    void SetParticlesPlaying(bool shouldPlay, bool force)
    {
        if (!force && visualsArePlaying == shouldPlay)
            return;

        for (int i = 0; i < frostParticles.Length; i++)
        {
            ParticleSystem particle = frostParticles[i];
            if (particle == null)
                continue;

            if (shouldPlay)
            {
                if (!particle.isPlaying)
                    particle.Play(true);
            }
            else
            {
                if (particle.isPlaying || particle.particleCount > 0)
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        visualsArePlaying = shouldPlay;
    }

    void ApplyVisualStyle()
    {
        if (!autoConfigureParticles || frostParticles == null || frostParticles.Length == 0)
            return;

        float minLifetime = Mathf.Max(0.1f, Mathf.Min(lifetimeRange.x, lifetimeRange.y));
        float maxLifetime = Mathf.Max(minLifetime, Mathf.Max(lifetimeRange.x, lifetimeRange.y));
        float minSize = Mathf.Max(0.06f, Mathf.Min(sizeRange.x, sizeRange.y));
        float maxSize = Mathf.Max(minSize + 0.02f, Mathf.Max(sizeRange.x, sizeRange.y));
        Vector2 normalizedWindDirection = windDirection.sqrMagnitude > 0.0001f
            ? windDirection.normalized
            : new Vector2(1f, -1f).normalized;
        float clampedWindSpeed = Mathf.Max(0.1f, windSpeed);
        float windX = normalizedWindDirection.x * clampedWindSpeed;
        float windY = normalizedWindDirection.y * clampedWindSpeed;

        for (int i = 0; i < frostParticles.Length; i++)
        {
            ParticleSystem particle = frostParticles[i];
            if (particle == null)
                continue;

            var main = particle.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1500;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = particleTint;

            var emission = particle.emission;
            emission.enabled = true;
            emission.rateOverTimeMultiplier = Mathf.Max(8f, baseEmissionRate);

            var shape = particle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = GetParticleBoxShapeScale();

            var velocityOverLifetime = particle.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(windX);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(windY);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.sortingOrder = Mathf.Max(particleRenderer.sortingOrder, 10000);
                particleRenderer.maxParticleSize = Mathf.Max(0.5f, particleRenderer.maxParticleSize);
            }

            baseEmissionByParticle[particle] = emission.rateOverTimeMultiplier;
        }
    }

    Vector3 GetParticleBoxShapeScale()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider == null)
            return new Vector3(1f, 1f, 0.1f);

        if (zoneCollider is BoxCollider2D boxCollider)
        {
            Vector2 size = boxCollider.size;
            return new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), 0.1f);
        }

        Vector3 worldSize = zoneCollider.bounds.size;
        Vector3 lossyScale = transform.lossyScale;
        float sx = Mathf.Abs(lossyScale.x) <= 0.0001f ? 1f : Mathf.Abs(lossyScale.x);
        float sy = Mathf.Abs(lossyScale.y) <= 0.0001f ? 1f : Mathf.Abs(lossyScale.y);
        return new Vector3(
            Mathf.Max(0.1f, worldSize.x / sx),
            Mathf.Max(0.1f, worldSize.y / sy),
            0.1f
        );
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        damageAmount = Mathf.Max(1, damageAmount);
        damageInterval = Mathf.Max(0.05f, damageInterval);
        baseEmissionRate = Mathf.Max(0.1f, baseEmissionRate);
        windSpeed = Mathf.Max(0f, windSpeed);

        EnsureTriggerCollider();
        ResolveVisualReferences();
        ApplyVisualStyle();
    }
#endif
}
