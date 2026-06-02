using UnityEngine;

[DisallowMultipleComponent]
public class CraftingStation : MonoBehaviour
{
    public CraftStationType stationType = CraftStationType.None;
    [SerializeField] private string displayName = "Станция крафта";
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;

    public bool TryInteract(Transform interactor, InventoryUI inventoryUI)
    {
        if (!CanInteract(interactor) || inventoryUI == null)
            return false;

        inventoryUI.OpenCrafting(stationType, this);
        return true;
    }

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null)
            return false;

        float maxDistance = Mathf.Max(0.2f, interactDistance);
        return Vector2.Distance(interactor.position, transform.position) <= maxDistance;
    }

    public CraftStationType StationType => stationType;
    public string DisplayName => displayName;

#if UNITY_EDITOR
    void OnValidate()
    {
        interactDistance = Mathf.Max(0.2f, interactDistance);
    }
#endif
}
