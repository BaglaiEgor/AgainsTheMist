using UnityEngine;

[System.Obsolete("FogRepellerManager is deprecated. Use FogSystem for fog logic and FogMaskDrawer for visuals.")]
public class FogRepellerManager : MonoBehaviour
{
    [System.Obsolete("FogRepellerManager.Register is deprecated. Use FogSystem.Instance.RegisterRepeller instead.")]
    public static void Register(FogRepeller r) { }

    [System.Obsolete("FogRepellerManager.Unregister is deprecated. Use FogSystem.Instance.UnregisterRepeller instead.")]
    public static void Unregister(FogRepeller r) { }

    void OnEnable()
    {
        enabled = false;
    }
}
