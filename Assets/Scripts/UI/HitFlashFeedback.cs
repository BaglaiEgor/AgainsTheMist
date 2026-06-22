using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class HitFlashFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;
    [Header("Transform Feedback")]
    [SerializeField] private bool punchScale = true;
    [SerializeField] private Vector3 punchScaleAmount = new Vector3(0.08f, 0.08f, 0f);
    [SerializeField] private bool shakePosition = true;
    [SerializeField] private Vector3 shakeStrength = new Vector3(0.035f, 0.035f, 0f);

    private Sequence flashTween;
    private Tween punchTween;
    private Tween shakeTween;
    private Color[] baseColors;
    private Vector3 baseScale = Vector3.one;
    private Vector3 baseLocalPosition = Vector3.zero;

    public static void PlayOn(GameObject target, Color color, float duration = 0.12f)
    {
        if (target == null)
            return;

        HitFlashFeedback feedback = target.GetComponent<HitFlashFeedback>();
        if (feedback == null)
            feedback = target.AddComponent<HitFlashFeedback>();

        feedback.Flash(color, duration);
    }

    public static void PlayOn(GameObject target, Color color, float duration, bool transformFeedback)
    {
        if (target == null)
            return;

        HitFlashFeedback feedback = target.GetComponent<HitFlashFeedback>();
        if (feedback == null)
            feedback = target.AddComponent<HitFlashFeedback>();

        feedback.Flash(color, duration, transformFeedback);
    }

    public void Flash(Color hitColor, float duration = 0.12f)
    {
        Flash(hitColor, duration, true);
    }

    public void Flash(Color hitColor, float duration, bool transformFeedback)
    {
        EnsureRenderers();
        if (renderers == null || renderers.Length == 0)
            return;

        KillFlash();
        EnsureBaseColors();

        float halfDuration = Mathf.Max(0.01f, duration) * 0.5f;
        flashTween = DOTween.Sequence();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
                continue;

            Color baseColor = i < baseColors.Length ? baseColors[i] : spriteRenderer.color;
            flashTween.Join(spriteRenderer.DOColor(hitColor, halfDuration));
            flashTween.Insert(halfDuration, spriteRenderer.DOColor(baseColor, halfDuration));
        }

        if (transformFeedback)
            PlayTransformFeedback(duration);
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        baseLocalPosition = transform.localPosition;
        EnsureRenderers();
        EnsureBaseColors();
    }

    private void OnDisable()
    {
        KillFlash();
        KillTransformTweens();
        RestoreBaseColors();
        transform.localScale = baseScale;
        transform.localPosition = baseLocalPosition;
    }

    private void EnsureRenderers()
    {
        if (renderers != null && renderers.Length > 0)
            return;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void EnsureBaseColors()
    {
        if (renderers == null)
            return;

        if (baseColors != null && baseColors.Length == renderers.Length)
            return;

        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
    }

    private void RestoreBaseColors()
    {
        if (renderers == null || baseColors == null)
            return;

        for (int i = 0; i < renderers.Length && i < baseColors.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = baseColors[i];
        }
    }

    private void KillFlash()
    {
        if (flashTween == null)
            return;

        flashTween.Kill();
        flashTween = null;
    }

    private void PlayTransformFeedback(float duration)
    {
        KillTransformTweens();

        if (punchScale)
        {
            transform.localScale = baseScale;
            punchTween = transform
                .DOPunchScale(punchScaleAmount, Mathf.Max(0.04f, duration), 6, 0.45f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => transform.localScale = baseScale);
        }

        if (shakePosition)
        {
            transform.localPosition = baseLocalPosition;
            shakeTween = transform
                .DOShakePosition(Mathf.Max(0.04f, duration), shakeStrength, 8, 45f, false, true)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => transform.localPosition = baseLocalPosition);
        }
    }

    private void KillTransformTweens()
    {
        punchTween?.Kill();
        shakeTween?.Kill();
        punchTween = null;
        shakeTween = null;
    }
}
