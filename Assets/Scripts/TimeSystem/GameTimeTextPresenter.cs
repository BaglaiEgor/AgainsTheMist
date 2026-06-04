using TMPro;
using UnityEngine;

public class GameTimeTextPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameTimeSystem timeSystem;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Format")]
    [SerializeField] private bool showDay = true;
    [SerializeField] private string dayPrefix = "День";
    [SerializeField] private string fallbackText = "--:--";

    private void OnEnable()
    {
        if (timeSystem != null)
            timeSystem.OnMinuteChanged += OnMinuteChanged;

        RefreshNow();
    }

    private void OnDisable()
    {
        if (timeSystem != null)
            timeSystem.OnMinuteChanged -= OnMinuteChanged;
    }

    public void RefreshNow()
    {
        if (timeText == null)
            return;

        if (timeSystem == null)
        {
            timeText.text = fallbackText;
            return;
        }

        ApplyText(timeSystem.Day, timeSystem.Hour, timeSystem.Minute);
    }

    private void OnMinuteChanged(int day, int hour, int minute)
    {
        ApplyText(day, hour, minute);
    }

    private void ApplyText(int day, int hour, int minute)
    {
        if (timeText == null)
            return;

        if (showDay)
        {
            timeText.text = $"{dayPrefix} {day} \n {hour:00}:{minute:00}";
            return;
        }

        timeText.text = $"{hour:00}:{minute:00}";
    }
}
