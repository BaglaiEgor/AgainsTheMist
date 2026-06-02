using UnityEngine;

public class LighthouseKeeperNPC : MonoBehaviour
{
    [SerializeField] private LighthouseKeeperUI keeperUI;
    [SerializeField] private float interactRadius = 2f;
    [SerializeField] private Vector2 rewardDropOffset = new Vector2(0.7f, -0.2f);

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return false;

        return Vector2.Distance(transform.position, interactor.position) <= interactRadius;
    }

    public bool TryInteract(Transform interactor, InventoryUI inventoryUI)
    {
        if (!CanInteract(interactor) || inventoryUI == null)
            return false;

        inventoryUI.OpenKeeper(this);
        return true;
    }

    public bool TrySpawnQuestReward(LighthouseKeeperQuest quest)
    {
        if (quest == null || quest.RewardPickupPrefab == null)
            return false;

        Vector3 position = transform.position + new Vector3(rewardDropOffset.x, rewardDropOffset.y, 0f);
        GameObject reward = Instantiate(quest.RewardPickupPrefab, position, Quaternion.identity);

        PickupItem pickup = reward.GetComponent<PickupItem>();
        if (pickup != null)
            pickup.amount = quest.RewardAmount;

        return true;
    }

    public LighthouseKeeperUI KeeperUI => keeperUI;
}
