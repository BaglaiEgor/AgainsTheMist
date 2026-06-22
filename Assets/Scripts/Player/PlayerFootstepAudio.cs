using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class PlayerFootstepAudio : MonoBehaviour
{
    [Header("Snow Check")]
    [SerializeField] private Tilemap snowTilemap;
    [SerializeField] private LayerMask snowLayer;
    [SerializeField] private float snowCheckRadius = 0.12f;

    [Header("Step Timing")]
    [SerializeField] private float stepDistance = 0.42f;
    [SerializeField] private float minMoveDistance = 0.02f;

    private readonly Collider2D[] snowHits = new Collider2D[4];
    private Vector3 lastPosition;
    private float distanceSinceStep;

    private void Awake()
    {
        if (snowLayer.value == 0)
        {
            int snowLayerIndex = LayerMask.NameToLayer("Snow");
            if (snowLayerIndex >= 0)
                snowLayer = 1 << snowLayerIndex;
        }

        lastPosition = transform.position;
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        distanceSinceStep = 0f;
    }

    private void Update()
    {
        Vector3 currentPosition = transform.position;
        Vector2 delta = currentPosition - lastPosition;
        float distance = delta.magnitude;

        lastPosition = currentPosition;

        if (distance < minMoveDistance)
            return;

        distanceSinceStep += distance;
        float safeStepDistance = Mathf.Max(0.05f, stepDistance);

        if (distanceSinceStep < safeStepDistance)
            return;

        distanceSinceStep %= safeStepDistance;
        AudioController.Instance?.PlayFootstep(IsOnSnow(currentPosition));
    }

    private bool IsOnSnow(Vector3 worldPosition)
    {
        if (snowTilemap != null)
        {
            Vector3Int cell = snowTilemap.WorldToCell(worldPosition);
            if (snowTilemap.HasTile(cell))
                return true;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(snowLayer);
        filter.useTriggers = true;

        return Physics2D.OverlapCircle(worldPosition, snowCheckRadius, filter, snowHits) > 0;
    }
}
