using UnityEngine;

public class FogRepeller : MonoBehaviour
{
    public float clearRadius = 5f;
    public Transform drawPoint;
    public bool showGizmos = true;
    public bool createsSafeZone = true;

    void OnEnable() { Register(); }
    void OnDisable() { Unregister(); }
    void OnDestroy() { Unregister(); }
    void Update() { Register(); }

    void Register()
    {
        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem != null)
            fogSystem.RegisterRepeller(this);
    }

    void Unregister()
    {
        if (FogSystem.TryGetExistingInstance(out FogSystem fogSystem))
            fogSystem.UnregisterRepeller(this);
    }

    public Vector3 GetDrawPosition()
    {
        return drawPoint != null ? drawPoint.position : transform.position;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Vector3 pos = GetDrawPosition();
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(pos, clearRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pos, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 pos = GetDrawPosition();
        Gizmos.color = new Color(1, 0.8f, 0, 0.5f);
        Gizmos.DrawSphere(pos, clearRadius);
    }
#endif
}
