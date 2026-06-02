using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FrostPoiChest : MonoBehaviour
{
    [Header("Interaction")]
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;
    [SerializeField] private bool oneTimeUse = true;

    [Header("Reward")]
    [SerializeField] private ItemData rewardItem;
    [Min(1)] [SerializeField] private int rewardAmount = 3;
    [SerializeField] private string fallbackRewardPath = "Materials/FrostEssence";

    [Header("Visual")]
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private Color closedColor = new Color(0.9f, 0.76f, 0.37f, 0.97f);
    [SerializeField] private Color openedColor = new Color(0.62f, 0.73f, 0.86f, 0.92f);
    [SerializeField] private Vector3 openedScale = new Vector3(0.52f, 0.24f, 1f);

    private bool opened;
    private Vector3 closedScale = Vector3.one;
    private bool cachedClosedScale;

    void Awake()
    {
        EnsureColliderTrigger();
        CacheClosedScale();
        ApplyVisualState();
    }

    public bool TryInteract(Transform interactor, Inventory inventory)
    {
        if (inventory == null || interactor == null)
            return false;

        if (oneTimeUse && opened)
            return false;

        float maxDistance = Mathf.Max(0.2f, interactDistance);
        if (Vector2.Distance(interactor.position, transform.position) > maxDistance)
            return false;

        ItemData resolvedReward = ResolveRewardItem();
        if (resolvedReward == null)
        {
            Debug.LogWarning("Frost POI chest has no reward item configured.");
            return false;
        }

        inventory.Add(resolvedReward, Mathf.Max(1, rewardAmount));
        opened = true;
        ApplyVisualState();
        return true;
    }

    ItemData ResolveRewardItem()
    {
        if (rewardItem != null)
            return rewardItem;

        if (string.IsNullOrWhiteSpace(fallbackRewardPath))
            return null;

        rewardItem = Resources.Load<ItemData>(fallbackRewardPath);
        return rewardItem;
    }

    void EnsureColliderTrigger()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    void CacheClosedScale()
    {
        if (cachedClosedScale)
            return;

        closedScale = transform.localScale;
        cachedClosedScale = true;
    }

    void ApplyVisualState()
    {
        if (chestRenderer == null)
            chestRenderer = GetComponent<SpriteRenderer>();

        if (chestRenderer != null)
            chestRenderer.color = opened ? openedColor : closedColor;

        transform.localScale = opened ? openedScale : closedScale;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        interactDistance = Mathf.Max(0.2f, interactDistance);
        rewardAmount = Mathf.Max(1, rewardAmount);
    }
#endif
}
