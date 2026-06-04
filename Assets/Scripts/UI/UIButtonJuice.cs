using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float hoverDuration = 0.1f;
    [SerializeField] private float clickPunchScale = 0.08f;
    [SerializeField] private float clickPunchDuration = 0.12f;
    [SerializeField] private bool ignoreNonInteractable = true;

    private Button button;
    private Tween scaleTween;
    private Vector3 baseScale = Vector3.one;
    private bool isHovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (CanAnimate())
            AnimateScale(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        AnimateScale(1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        PlayClickFeedback();
    }

    private void OnDisable()
    {
        ResetScale();
    }

    private void OnDestroy()
    {
        ResetScale();
    }

    private bool CanAnimate()
    {
        return !ignoreNonInteractable || button == null || button.interactable;
    }

    private void AnimateScale(float targetScale)
    {
        KillScaleTween();
        scaleTween = transform
            .DOScale(baseScale * Mathf.Max(0.01f, targetScale), Mathf.Max(0.01f, hoverDuration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void PlayClickFeedback()
    {
        if (!CanAnimate())
            return;

        KillScaleTween();
        transform.localScale = baseScale * (isHovered ? Mathf.Max(0.01f, hoverScale) : 1f);
        scaleTween = transform
            .DOPunchScale(baseScale * Mathf.Max(0f, clickPunchScale), Mathf.Max(0.01f, clickPunchDuration), 6, 0.45f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => transform.localScale = baseScale * (isHovered ? Mathf.Max(0.01f, hoverScale) : 1f));
    }

    private void KillScaleTween()
    {
        if (scaleTween == null)
            return;

        scaleTween.Kill();
        scaleTween = null;
    }

    private void ResetScale()
    {
        isHovered = false;
        KillScaleTween();
        transform.localScale = baseScale;
    }
}
