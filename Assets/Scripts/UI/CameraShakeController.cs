using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraShakeController : MonoBehaviour
{
    private static CameraShakeController instance;

    private Tween shakeTween;
    private Vector3 baseLocalPosition;
    private float nextShakeTime;

    public static void ShakeMain(float duration = 0.1f, float strength = 0.06f)
    {
        CameraShakeController controller = Resolve();
        if (controller != null)
            controller.Shake(duration, strength);
    }

    public static void StopShake()
    {
        CameraShakeController controller = Resolve();
        if (controller != null)
            controller.KillShake();
    }

    public void Shake(float duration = 0.1f, float strength = 0.06f)
    {
        if (Time.time < nextShakeTime)
            return;

        nextShakeTime = Time.time + 0.06f;
        KillShake();

        baseLocalPosition = transform.localPosition;
        shakeTween = transform
            .DOShakePosition(Mathf.Max(0.01f, duration), Mathf.Max(0f, strength), 8, 45f, false, true)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => transform.localPosition = baseLocalPosition);
    }

    private static CameraShakeController Resolve()
    {
        if (instance != null)
            return instance;

        Camera camera = Camera.main;
        if (camera == null)
            return null;

        instance = camera.GetComponent<CameraShakeController>();
        if (instance == null)
            instance = camera.gameObject.AddComponent<CameraShakeController>();

        return instance;
    }

    private void Awake()
    {
        instance = this;
        baseLocalPosition = transform.localPosition;
    }

    private void OnDisable()
    {
        KillShake();
    }

    private void KillShake()
    {
        if (shakeTween != null)
        {
            shakeTween.Kill();
            shakeTween = null;
        }

        transform.localPosition = baseLocalPosition;
    }
}
