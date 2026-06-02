using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PotionEffectIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI timerText;

    private PotionEffectsUI.EffectSlot slot;
    private Action<PotionEffectsUI.EffectSlot> onEnter;
    private Action<PotionEffectsUI.EffectSlot> onExit;

    void Awake()
    {
        EnsureReferences();
    }

    public void Initialize(
        PotionEffectsUI.EffectSlot slot,
        Action<PotionEffectsUI.EffectSlot> onEnter,
        Action<PotionEffectsUI.EffectSlot> onExit)
    {
        this.slot = slot;
        this.onEnter = onEnter;
        this.onExit = onExit;

        EnsureReferences();
    }

    public void SetData(Sprite sprite, int remainingSeconds)
    {
        EnsureReferences();

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = Color.white;
        }

        if (timerText != null)
            timerText.text = Mathf.Max(0, remainingSeconds).ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onEnter?.Invoke(slot);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onExit?.Invoke(slot);
    }

    private void EnsureReferences()
    {
        if (icon == null)
            icon = GetComponentInChildren<Image>();

        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>();
    }
}
