using System.Collections;
using UnityEngine;

public class SnowTrailStamp : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float fadeTime = 1f;

    private Coroutine fadeRoutine;
    private bool holdUntilReleased;
    private bool isReleased;

    public void Initialize(float newLifetime, float newFadeTime)
    {
        Initialize(newLifetime, newFadeTime, false);
    }

    public void Initialize(float newLifetime, float newFadeTime, bool newHoldUntilReleased)
    {
        lifetime = Mathf.Max(0f, newLifetime);
        fadeTime = Mathf.Max(0.01f, newFadeTime);
        holdUntilReleased = newHoldUntilReleased;
        isReleased = !holdUntilReleased;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeAndDestroy());
    }

    public void Release()
    {
        isReleased = true;
    }

    void OnEnable()
    {
        if (fadeRoutine == null)
            fadeRoutine = StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i].color;

        if (holdUntilReleased)
        {
            while (!isReleased)
                yield return null;

            yield return FadeRenderers(renderers, startColors, fadeTime);
            Destroy(gameObject);
            yield break;
        }

        float safeLifetime = Mathf.Max(0.01f, lifetime);
        yield return FadeRenderers(renderers, startColors, safeLifetime);

        Destroy(gameObject);
    }

    private IEnumerator FadeRenderers(SpriteRenderer[] renderers, Color[] startColors, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            float alphaMultiplier = 1f - elapsed / safeDuration;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = startColors[i];
                color.a *= alphaMultiplier;
                renderers[i].color = color;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
