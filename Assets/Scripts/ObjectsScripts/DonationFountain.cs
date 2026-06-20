using System.Collections.Generic;
using TMPro;
using UnityEngine;

public readonly struct DonationFountainSnapshot
{
    public readonly int level;
    public readonly int coinsInCurrentLevel;

    public DonationFountainSnapshot(int level, int coinsInCurrentLevel)
    {
        this.level = Mathf.Max(0, level);
        this.coinsInCurrentLevel = Mathf.Max(0, coinsInCurrentLevel);
    }
}

[DisallowMultipleComponent]
public class DonationFountain : MonoBehaviour
{
    [System.Serializable]
    public class Reward
    {
        public ItemData item;
        [Min(1)] public int amount = 1;
    }

    [System.Serializable]
    public class FountainLevel
    {
        [Min(1)] public int requiredCoins = 1;
        public Reward[] rewards;
    }

    private struct SimulatedSlot
    {
        public ItemData item;
        public int amount;
    }

    [Header("Interaction")]
    [SerializeField] private ItemData coinItem;
    [SerializeField] private Collider2D interactionCollider;
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;

    [Header("Counter")]
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private bool autoCreateCounterText = true;

    [Header("Levels")]
    [SerializeField] private List<FountainLevel> levels = new();

    [Header("Runtime")]
    [SerializeField] private int currentLevel;
    [SerializeField] private int coinsInCurrentLevel;

    private Collider2D[] interactionColliders;

    public string InteractLabel => "Пожертвовать";
    public int CurrentLevel => Mathf.Clamp(currentLevel, 0, MaxLevel);
    public int MaxLevel => levels != null ? levels.Count : 0;
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    private void Awake()
    {
        if (coinItem == null)
            coinItem = Resources.Load<ItemData>("Materials/Coin");

        CacheInteractionColliders();
        EnsureCounterText();
        ClampState();
        RefreshCounter();
    }

    private void OnValidate()
    {
        ClampState();
    }

    public bool CanInteract(Transform interactor)
    {
        if (IsMaxLevel)
            return false;

        if (interactor == null)
            return true;

        return GetDistanceToInteractionArea(interactor.position) <= interactDistance;
    }

    public bool CanShowTooltip(Transform interactor)
    {
        Inventory inventory = FindInventory(interactor);
        return CanInteract(interactor) && coinItem != null && inventory != null && inventory.HasItem(coinItem, 1);
    }

    public bool TryDonate(Transform interactor, Inventory inventory)
    {
        if (!CanInteract(interactor) || inventory == null || coinItem == null || !inventory.HasItem(coinItem, 1))
            return false;

        FountainLevel level = GetCurrentLevel();
        if (level == null)
            return false;

        int nextCoins = coinsInCurrentLevel + 1;
        bool completesLevel = nextCoins >= Mathf.Max(1, level.requiredCoins);
        if (completesLevel && !CanAddRewards(inventory, level))
            return false;

        if (!InventoryTransactionService.TryConsume(inventory, coinItem, 1))
            return false;

        coinsInCurrentLevel = nextCoins;

        if (completesLevel)
        {
            GiveRewards(inventory, level);
            currentLevel++;
            coinsInCurrentLevel = 0;
        }

        ClampState();
        RefreshCounter();
        AudioController.Instance?.PlayInteract(transform.position);
        return true;
    }

    public DonationFountainSnapshot CreateSnapshot()
    {
        ClampState();
        return new DonationFountainSnapshot(currentLevel, coinsInCurrentLevel);
    }

    public void RestoreSnapshot(DonationFountainSnapshot snapshot)
    {
        currentLevel = snapshot.level;
        coinsInCurrentLevel = snapshot.coinsInCurrentLevel;
        ClampState();
        RefreshCounter();
    }

    private FountainLevel GetCurrentLevel()
    {
        if (levels == null || currentLevel < 0 || currentLevel >= levels.Count)
            return null;

        return levels[currentLevel];
    }

    private bool CanAddRewards(Inventory inventory, FountainLevel level)
    {
        if (inventory == null || level == null || level.rewards == null || level.rewards.Length == 0)
            return true;

        SimulatedSlot[] slots = new SimulatedSlot[inventory.SlotCount];
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventoryItem slot = inventory.GetItem(i);
            if (slot == null || slot.IsEmpty || slot.item == null)
                continue;

            slots[i].item = slot.item;
            slots[i].amount = slot.amount;
        }

        for (int i = 0; i < level.rewards.Length; i++)
        {
            Reward reward = level.rewards[i];
            if (reward == null || reward.item == null || reward.amount <= 0)
                continue;

            if (!TrySimulateAdd(slots, reward.item, reward.amount))
                return false;
        }

        return true;
    }

    private static bool TrySimulateAdd(SimulatedSlot[] slots, ItemData item, int amount)
    {
        int remaining = amount;
        int maxStack = GetMaxStack(item);

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].item != item)
                continue;

            int add = Mathf.Min(maxStack - slots[i].amount, remaining);
            if (add <= 0)
                continue;

            slots[i].amount += add;
            remaining -= add;
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].item != null)
                continue;

            int add = Mathf.Min(maxStack, remaining);
            slots[i].item = item;
            slots[i].amount = add;
            remaining -= add;
        }

        return remaining <= 0;
    }

    private static int GetMaxStack(ItemData item)
    {
        if (item == null)
            return 1;

        if (item.type == ItemType.Equipment)
            return 1;

        return Mathf.Max(1, item.maxStack);
    }

    private void GiveRewards(Inventory inventory, FountainLevel level)
    {
        if (inventory == null || level == null || level.rewards == null)
            return;

        for (int i = 0; i < level.rewards.Length; i++)
        {
            Reward reward = level.rewards[i];
            if (reward == null || reward.item == null || reward.amount <= 0)
                continue;

            InventoryTransactionService.TryInsertItem(inventory, reward.item, reward.amount);
        }
    }

    private void ClampState()
    {
        if (levels == null)
            levels = new List<FountainLevel>();

        currentLevel = Mathf.Clamp(currentLevel, 0, levels.Count);

        FountainLevel level = GetCurrentLevel();
        if (level == null)
        {
            coinsInCurrentLevel = 0;
            return;
        }

        int required = Mathf.Max(1, level.requiredCoins);
        coinsInCurrentLevel = Mathf.Clamp(coinsInCurrentLevel, 0, required - 1);
    }

    private void RefreshCounter()
    {
        if (counterText == null)
            return;

        if (levels == null || levels.Count == 0)
        {
            counterText.text = "0/0";
            return;
        }

        if (IsMaxLevel)
        {
            FountainLevel lastLevel = levels[Mathf.Max(0, levels.Count - 1)];
            int required = lastLevel != null ? Mathf.Max(1, lastLevel.requiredCoins) : 0;
            counterText.text = $"{required}/{required}";
            return;
        }

        FountainLevel level = GetCurrentLevel();
        int need = level != null ? Mathf.Max(1, level.requiredCoins) : 0;
        counterText.text = $"{coinsInCurrentLevel}/{need}";
    }

    private void EnsureCounterText()
    {
        if (counterText != null || !autoCreateCounterText)
            return;

        GameObject textObject = new GameObject("DonationCounterText", typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 1f, 0f);

        counterText = textObject.GetComponent<TMP_Text>();
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.fontSize = 3f;
        counterText.color = Color.white;
    }

    private void CacheInteractionColliders()
    {
        if (interactionCollider != null)
        {
            interactionColliders = new[] { interactionCollider };
            return;
        }

        interactionColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private float GetDistanceToInteractionArea(Vector2 point)
    {
        float bestDistance = Vector2.Distance(point, transform.position);

        if (interactionColliders == null || interactionColliders.Length == 0)
            CacheInteractionColliders();

        if (interactionColliders == null)
            return bestDistance;

        for (int i = 0; i < interactionColliders.Length; i++)
        {
            Collider2D currentCollider = interactionColliders[i];
            if (currentCollider == null || !currentCollider.enabled || !currentCollider.gameObject.activeInHierarchy)
                continue;

            Vector2 closestPoint = currentCollider.ClosestPoint(point);
            float distance = Vector2.Distance(point, closestPoint);
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }

    private static Inventory FindInventory(Transform source)
    {
        if (source == null)
            return null;

        Inventory inventory = source.GetComponent<Inventory>();
        if (inventory == null)
            inventory = source.GetComponentInParent<Inventory>();
        if (inventory == null)
            inventory = source.GetComponentInChildren<Inventory>();

        return inventory;
    }
}
