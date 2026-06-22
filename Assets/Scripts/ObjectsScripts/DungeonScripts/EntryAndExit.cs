using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EntryAndExit : MonoBehaviour
{
    [Header("Teleport")]
    [SerializeField] private GameObject tpPoint;
    [Min(0.2f)] [SerializeField] private float interactDistance = 1.6f;

    [Header("Dungeon")]
    [SerializeField] private Tilemap dungeonTilemap;
    [SerializeField] private GameObject dungeonObjectsRoot;
    [SerializeField] private DayNightLightController dayNightLightController;
    [SerializeField] private float dungeonGlobalLightIntensity = 0.7f;
    [SerializeField] private Color dungeonGlobalLightColor = new Color(1f, 0.94f, 0.82f, 1f);

    [Header("Camera")]
    [SerializeField] private CinemachineConfiner2D cameraConfiner;
    [SerializeField] private Collider2D cameraBounds;

    public bool isEntry;

    public static bool IsPlayerInDungeon { get; private set; }

    public string InteractLabel => isEntry ? "Войти" : "Выйти";

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null || tpPoint == null)
            return false;

        return Vector2.Distance(interactor.position, transform.position) <= interactDistance;
    }

    public bool TryInteract(Transform interactor)
    {
        if (!TryGetTeleportPosition(interactor, out Vector3 targetPosition))
            return false;

        Rigidbody2D rb = interactor.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.position = targetPosition;

        interactor.position = targetPosition;
        return true;
    }

    public bool TryGetTeleportPosition(Transform interactor, out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;
        if (!CanInteract(interactor))
            return false;

        targetPosition = GetTeleportTargetPosition();
        return true;
    }

    private Vector3 GetTeleportTargetPosition()
    {
        Collider2D targetCollider = tpPoint.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = tpPoint.GetComponentInChildren<Collider2D>();

        if (targetCollider == null)
            return tpPoint.transform.position;

        Bounds bounds = targetCollider.bounds;
        return new Vector3(bounds.center.x, bounds.min.y, tpPoint.transform.position.z);
    }

    public void ApplyDungeonState()
    {
        IsPlayerInDungeon = isEntry;

        if (isEntry)
            DestroyFogEnemies();

        if (dayNightLightController == null)
            dayNightLightController = FindFirstObjectByType<DayNightLightController>();

        if (dayNightLightController != null)
        {
            if (isEntry)
                dayNightLightController.ForceLight(dungeonGlobalLightIntensity, dungeonGlobalLightColor);
            else
                dayNightLightController.ClearForcedIntensity();
        }

        if (dungeonTilemap != null)
            dungeonTilemap.gameObject.SetActive(isEntry);

        if (dungeonObjectsRoot != null)
            dungeonObjectsRoot.SetActive(isEntry);

        DungeonPostProcessController postProcessController = FindFirstObjectByType<DungeonPostProcessController>();
        if (postProcessController != null)
            postProcessController.SetDungeonMode(isEntry);

        ApplyCameraConfiner();
    }

    private void ApplyCameraConfiner()
    {
        if (cameraConfiner == null)
            cameraConfiner = FindFirstObjectByType<CinemachineConfiner2D>(FindObjectsInactive.Include);

        if (cameraConfiner == null)
            return;

        cameraConfiner.BoundingShape2D = isEntry ? cameraBounds : null;
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private void DestroyFogEnemies()
    {
        FogEnemy[] fogEnemies = FindObjectsByType<FogEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fogEnemies.Length; i++)
        {
            if (fogEnemies[i] != null)
                Destroy(fogEnemies[i].gameObject);
        }
    }
}
