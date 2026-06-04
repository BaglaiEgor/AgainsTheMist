using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class UIPanelJuice : MonoBehaviour
{
    [SerializeField] private float showScale = 0.96f;
    [SerializeField] private float duration = 0.12f;

    private CanvasGroup canvasGroup;
    private Tween tween;
    private Vector3 baseScale = Vector3.one;
    private bool initialized;

    public static UIPanelJuice EnsureOn(GameObject target)
    {
        if (target == null)
            return null;

        UIPanelJuice juice = target.GetComponent<UIPanelJuice>();
        if (juice == null)
            juice = target.AddComponent<UIPanelJuice>();

        juice.EnsureInitialized();
        return juice;
    }

    public static void SetVisible(GameObject target, bool visible, bool animate = true)
    {
        UIPanelJuice juice = EnsureOn(target);
        if (juice == null)
            return;

        if (animate)
        {
            if (visible)
                juice.PlayShow();
            else
                juice.PlayHideAndDisable();
            return;
        }

        if (visible)
            juice.ShowInstant();
        else
            juice.HideInstant();
    }

    public void PlayShow()
    {
        EnsureInitialized();
        KillTween();

        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = baseScale * Mathf.Clamp(showScale, 0.8f, 1f);

        tween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(1f, Mathf.Max(0.01f, duration)))
            .Join(transform.DOScale(baseScale, Mathf.Max(0.01f, duration)).SetEase(Ease.OutQuad))
            .SetUpdate(true);
    }

    public void PlayHideAndDisable()
    {
        EnsureInitialized();
        KillTween();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        tween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(0f, Mathf.Max(0.01f, duration)))
            .Join(transform.DOScale(baseScale * Mathf.Clamp(showScale, 0.8f, 1f), Mathf.Max(0.01f, duration)).SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                transform.localScale = baseScale;
                gameObject.SetActive(false);
            });
    }

    public void ShowInstant()
    {
        EnsureInitialized();
        KillTween();

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = baseScale;
    }

    public void HideInstant()
    {
        EnsureInitialized();
        KillTween();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        transform.localScale = baseScale;
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        KillTween();
        if (initialized)
            transform.localScale = baseScale;
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        baseScale = transform.localScale;
        initialized = true;
    }

    private void KillTween()
    {
        if (tween == null)
            return;

        tween.Kill();
        tween = null;
    }
}
