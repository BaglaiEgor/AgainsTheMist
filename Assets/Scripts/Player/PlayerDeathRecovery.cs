using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerDeathRecovery : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private Vector2 respawnPosition = new Vector2(0f, 16f);

    [Header("Fade")]
    [Min(0.05f)] [SerializeField] private float fadeOutDuration = 0.45f;
    [Min(0.05f)] [SerializeField] private float fadeInDuration = 0.55f;
    [Min(0f)] [SerializeField] private float blackHoldDuration = 0.08f;

    private PlayerHealth playerHealth;
    private PlayerVitals playerVitals;
    private PlayerController playerController;
    private Rigidbody2D rb;
    private CanvasGroup fadeCanvasGroup;
    private bool isRecovering;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerVitals = GetComponent<PlayerVitals>();
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.Died -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (isRecovering)
            return;

        StartCoroutine(RecoverRoutine());
    }

    private IEnumerator RecoverRoutine()
    {
        isRecovering = true;
        playerController?.SetMovementLocked(true);
        CameraShakeController.StopShake();
        EnsureFadeCanvas();

        yield return FadeTo(1f, fadeOutDuration);

        if (blackHoldDuration > 0f)
            yield return new WaitForSeconds(blackHoldDuration);

        TeleportToRespawn();
        RestoreVitals();

        yield return FadeTo(0f, fadeInDuration);

        playerController?.SetMovementLocked(false);
        isRecovering = false;
    }

    private void TeleportToRespawn()
    {
        Vector3 target = new Vector3(respawnPosition.x, respawnPosition.y, transform.position.z);
        transform.position = target;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = respawnPosition;
        }
    }

    private void RestoreVitals()
    {
        playerHealth?.Revive();
        playerVitals?.RestoreFull();
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvasGroup != null)
            return;

        GameObject canvasObject = new GameObject("DeathFadeCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(canvasObject.transform, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeCanvasGroup = canvasGroup;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
            yield break;

        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }
}
