using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LighthouseKeeperTipButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button button;

    private GuidanceEntry entry;
    private Action<GuidanceEntry> onClick;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (titleText == null)
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Setup(GuidanceEntry guidanceEntry, Action<GuidanceEntry> clickCallback)
    {
        entry = guidanceEntry;
        onClick = clickCallback;

        if (titleText != null)
            titleText.text = entry != null ? entry.Title : string.Empty;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            button.interactable = entry != null;
        }

        gameObject.SetActive(entry != null);
    }

    private void HandleClick()
    {
        if (entry != null)
            onClick?.Invoke(entry);
    }
}
