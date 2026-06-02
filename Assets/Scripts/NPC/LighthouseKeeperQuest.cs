using UnityEngine;

[CreateAssetMenu(fileName = "LighthouseKeeperQuest", menuName = "Farm/NPC/Lighthouse Keeper Quest")]
public class LighthouseKeeperQuest : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string title;
    [TextArea(2, 5)] [SerializeField] private string description;
    [SerializeField] private ItemData requiredItem;
    [Min(1)] [SerializeField] private int requiredAmount = 1;
    [SerializeField] private GameObject rewardPickupPrefab;
    [Min(1)] [SerializeField] private int rewardAmount = 1;

    public string Id => id;
    public string Title => title;
    public string Description => description;
    public ItemData RequiredItem => requiredItem;
    public int RequiredAmount => requiredAmount;
    public GameObject RewardPickupPrefab => rewardPickupPrefab;
    public int RewardAmount => rewardAmount;
}
