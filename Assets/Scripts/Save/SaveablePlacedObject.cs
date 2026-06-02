using UnityEngine;

[DisallowMultipleComponent]
public class SaveablePlacedObject : MonoBehaviour
{
    [SerializeField] private ItemData sourceItem;
    [SerializeField, Range(0, 3)] private int rotationSteps;

    public ItemData SourceItem => sourceItem;
    public int RotationSteps => rotationSteps;

    public void Initialize(ItemData item, int steps)
    {
        sourceItem = item;
        rotationSteps = ((steps % 4) + 4) % 4;
    }
}
