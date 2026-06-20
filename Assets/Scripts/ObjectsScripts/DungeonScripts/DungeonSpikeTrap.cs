using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonSpikeTrap : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite frame1;
    [SerializeField] private Sprite frame2;
    [SerializeField] private Sprite frame3;
    [SerializeField] private Sprite activeFrame4;

    [Header("Timing")]
    [SerializeField] private bool cycleSpikes = true;
    [Min(0.02f)] [SerializeField] private float frameTime = 0.15f;
    [Min(0f)] [SerializeField] private float idleTime = 1f;
    [Min(0.02f)] [SerializeField] private float activeTime = 0.5f;

    [Header("Damage")]
    [Min(0)] [SerializeField] private int damage = 10;
    [Min(0.02f)] [SerializeField] private float damageCooldown = 0.5f;

    private readonly List<PlayerHealth> playersInside = new();
    private readonly Dictionary<PlayerHealth, float> nextDamageTimeByPlayer = new();
    private bool isActive;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Collider2D damageCollider = GetComponent<Collider2D>();
        damageCollider.isTrigger = true;

        SetSprite(frame1);
    }

    private void OnEnable()
    {
        if (cycleSpikes)
        {
            StartCoroutine(TrapLoop());
            return;
        }

        SetAlwaysActive();
    }

    private void Update()
    {
        if (!isActive)
            return;

        ApplyDamageToPlayersInside();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playersInside.Contains(playerHealth))
            return;

        playersInside.Add(playerHealth);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        playersInside.Remove(playerHealth);
        nextDamageTimeByPlayer.Remove(playerHealth);
    }

    private IEnumerator TrapLoop()
    {
        while (enabled)
        {
            isActive = false;
            SetSprite(frame1);
            yield return new WaitForSeconds(idleTime);

            SetSprite(frame2);
            yield return new WaitForSeconds(frameTime);

            SetSprite(frame3);
            yield return new WaitForSeconds(frameTime);

            SetSprite(activeFrame4);
            isActive = true;
            AudioController.Instance?.PlayDungeonTrap(transform.position);
            ApplyDamageToPlayersInside();
            yield return new WaitForSeconds(activeTime);

            isActive = false;
            SetSprite(frame3);
            yield return new WaitForSeconds(frameTime);

            SetSprite(frame2);
            yield return new WaitForSeconds(frameTime);
        }
    }

    private void SetAlwaysActive()
    {
        isActive = true;
        SetSprite(activeFrame4);
        ApplyDamageToPlayersInside();
    }

    private void ApplyDamageToPlayersInside()
    {
        if (damage <= 0)
            return;

        for (int i = playersInside.Count - 1; i >= 0; i--)
        {
            PlayerHealth playerHealth = playersInside[i];
            if (playerHealth == null)
            {
                playersInside.RemoveAt(i);
                continue;
            }

            if (nextDamageTimeByPlayer.TryGetValue(playerHealth, out float nextDamageTime) && Time.time < nextDamageTime)
                continue;

            playerHealth.TakeDamage(damage);
            nextDamageTimeByPlayer[playerHealth] = Time.time + damageCooldown;
        }
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }
}
