using UnityEngine;

[DisallowMultipleComponent]
public class GuidanceUnlockTarget : MonoBehaviour
{
    [SerializeField] private GuidanceEntry guidanceEntry;
    [SerializeField] private Transform popupAnchor;
    [SerializeField] private float unlockDelay = 0.15f;
    [SerializeField] private Vector2 viewportPadding = new Vector2(0.04f, 0.04f);

    private float visibleTimer;
    private bool unlocked;

    private void Update()
    {
        if (unlocked || guidanceEntry == null || GuidanceSystem.Instance == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (!IsInCamera(camera))
        {
            visibleTimer = 0f;
            return;
        }

        visibleTimer += Time.deltaTime;
        if (visibleTimer < Mathf.Max(0f, unlockDelay))
            return;

        Transform targetAnchor = popupAnchor != null ? popupAnchor : transform;
        GuidanceSystem.Instance.Unlock(guidanceEntry, targetAnchor);
        unlocked = true;
    }

    private bool IsInCamera(Camera camera)
    {
        Vector3 viewportPoint = camera.WorldToViewportPoint(transform.position);
        if (viewportPoint.z < 0f)
            return false;

        float paddingX = Mathf.Clamp01(viewportPadding.x);
        float paddingY = Mathf.Clamp01(viewportPadding.y);
        return viewportPoint.x >= paddingX &&
               viewportPoint.x <= 1f - paddingX &&
               viewportPoint.y >= paddingY &&
               viewportPoint.y <= 1f - paddingY;
    }
}
