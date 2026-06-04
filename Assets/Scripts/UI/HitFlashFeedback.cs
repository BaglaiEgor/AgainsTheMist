using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class HitFlashFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;

    private Sequence flashTween;
    private Color[] baseColors;

    public static void PlayOn(GameObject target, Color color, float duration = 0.12f)
    {
        if (target == null)
            return;

        HitFlashFeedback feedback = target.GetComponent<HitFlashFeedback>();
        if (feedback == null)
            feedback = target.AddComponent<HitFlashFeedback>();

        feedback.Flash(color, duration);
    }

    public void Flash(Color hitColor, float duration = 0.12f)
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
    }

    private void Awake()
    {
        EnsureRenderers();
        EnsureBaseColors();
    }

    private void OnDisable()
    {
        KillFlash();
        RestoreBaseColors();
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
}
