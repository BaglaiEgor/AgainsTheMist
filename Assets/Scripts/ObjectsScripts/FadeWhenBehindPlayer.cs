using UnityEngine;

public class FadeWhenBehindPlayer : MonoBehaviour
{
    [Header("Fade Settings")]
    [Range(0f, 1f)] public float fadedAlpha = 0.4f;
    public float fadeSpeed = 8f;

    [Header("Player")]
    public string playerTag = "Player";

    [Header("Trigger")]
    [SerializeField] private Collider2D fadeTrigger;

    private SpriteRenderer[] renderers;
    private float targetAlpha = 1f;
    private int playerContacts;
    private Transform playerRoot;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        ResolvePlayerRoot();
        EnsureTriggerBinding();
    }

    void Update()
    {
        foreach (var r in renderers)
        {
            Color c = r.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
            r.color = c;
        }
    }

    public void HandleFadeTriggerEnter(Collider2D other)
    {
        if (!IsRootPlayerCollider(other))
            return;

        playerContacts++;
        targetAlpha = fadedAlpha;
    }

    public void HandleFadeTriggerExit(Collider2D other)
    {
        if (!IsRootPlayerCollider(other))
            return;

        playerContacts = Mathf.Max(0, playerContacts - 1);
        if (playerContacts == 0)
            targetAlpha = 1f;
    }

    void EnsureTriggerBinding()
    {
        if (fadeTrigger == null)
            fadeTrigger = FindTriggerOnSelfOrChildren();

        if (fadeTrigger == null)
        {
            Debug.LogWarning($"FadeWhenBehindPlayer: не найден триггер-коллайдер на объекте '{name}'.");
            return;
        }

        if (!fadeTrigger.isTrigger)
        {
            Debug.LogWarning($"FadeWhenBehindPlayer: fadeTrigger на объекте '{name}' должен иметь Is Trigger = true.");
        }

        FadeTriggerRelay relay = fadeTrigger.GetComponent<FadeTriggerRelay>();
        if (relay == null)
            relay = fadeTrigger.gameObject.AddComponent<FadeTriggerRelay>();

        relay.Bind(this);
    }

    void ResolvePlayerRoot()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        playerRoot = playerObject != null ? playerObject.transform : null;
    }

    bool IsRootPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (playerRoot == null || !playerRoot.CompareTag(playerTag))
            ResolvePlayerRoot();

        if (playerRoot == null)
            return false;

        // Accept only collider that belongs to the exact player root object.
        return other.transform == playerRoot;
    }

    Collider2D FindTriggerOnSelfOrChildren()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in colliders)
        {
            if (c.isTrigger)
                return c;
        }

        return null;
    }
}

public class FadeTriggerRelay : MonoBehaviour
{
    private FadeWhenBehindPlayer owner;

    public void Bind(FadeWhenBehindPlayer targetOwner)
    {
        owner = targetOwner;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        owner?.HandleFadeTriggerEnter(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        owner?.HandleFadeTriggerExit(other);
    }
}
