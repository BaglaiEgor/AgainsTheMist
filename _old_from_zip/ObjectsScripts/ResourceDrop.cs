using UnityEngine;

[System.Serializable]
public class Drop
{
    public GameObject prefab;
    public int minAmount = 1;
    public int maxAmount = 1;
    [Range(0f, 1f)]
    public float chance = 1f;
    public bool dropOnlyInFrostZone;
}

public class ResourceDrop : MonoBehaviour
{
    [SerializeField] private Drop[] drops;
    private bool hasDropped;

    public void DropNow()
    {
        if (hasDropped)
            return;

        hasDropped = true;

        foreach (Drop drop in drops)
        {
            if (drop == null || drop.prefab == null)
                continue;
            if (drop.dropOnlyInFrostZone && !FrostFogZone.IsAnyZoneActiveAtPosition(transform.position))
                continue;

            if (Random.value <= drop.chance)
            {
                int count = Random.Range(drop.minAmount, drop.maxAmount + 1);
                for (int i = 0; i < count; i++)
                {
                    Instantiate(drop.prefab, transform.position, Quaternion.identity);
                }
            }
        }
    }
}
