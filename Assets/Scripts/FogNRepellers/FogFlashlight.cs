using UnityEngine;

public class FogFlashlight : FogRepeller
{
    [Header("Параметры фонарика")]
    public float beamLength = 8f;          // длина луча
    public float beamAngle = 25f;          // угол (в градусах)

    public Vector3 Direction => transform.forward.normalized;
}
