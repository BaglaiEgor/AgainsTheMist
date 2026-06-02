using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class GuidanceTextUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI guidanceText;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (guidanceText == null)
            guidanceText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        GuidanceSystem.OnActiveGuidanceChanged += HandleGuidanceChanged;

        GuidanceSystem system = GuidanceSystem.Instance;
        if (system != null)
            HandleGuidanceChanged(system.ActiveEntry, system.ActiveText);
        else
            HandleGuidanceChanged(null, string.Empty);
    }

    private void OnDisable()
    {
        GuidanceSystem.OnActiveGuidanceChanged -= HandleGuidanceChanged;
    }

    private void HandleGuidanceChanged(GuidanceEntry _entry, string text)
    {
        bool hasText = !string.IsNullOrWhiteSpace(text);

        if (guidanceText != null)
            guidanceText.text = hasText ? text : string.Empty;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = hasText ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            if (guidanceText != null)
                guidanceText.enabled = hasText;
        }
    }
}
