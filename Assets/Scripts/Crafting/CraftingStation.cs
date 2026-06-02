using UnityEngine;

[DisallowMultipleComponent]
public class CraftingStation : MonoBehaviour
{
    public CraftStationType stationType = CraftStationType.None;
    [SerializeField] private string displayName = "Станция крафта";
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;
    private Collider2D[] stationColliders;

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
        if (IsCloseToAnyCollider(interactor.position, maxDistance))
            return true;

        return Vector2.Distance(interactor.position, transform.position) <= maxDistance;
    }

    public CraftStationType StationType => stationType;
    public string DisplayName => displayName;

    private bool IsCloseToAnyCollider(Vector3 worldPosition, float maxDistance)
    {
        if (stationColliders == null || stationColliders.Length == 0)
            stationColliders = GetComponentsInChildren<Collider2D>(true);

        float sqrMaxDistance = maxDistance * maxDistance;
        Vector2 point = worldPosition;

        for (int i = 0; i < stationColliders.Length; i++)
        {
            Collider2D stationCollider = stationColliders[i];
            if (stationCollider == null || !stationCollider.enabled)
                continue;

            Vector2 closestPoint = stationCollider.ClosestPoint(point);
            if ((closestPoint - point).sqrMagnitude <= sqrMaxDistance)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        interactDistance = Mathf.Max(0.2f, interactDistance);
        stationColliders = null;
    }
#endif
}
