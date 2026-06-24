using System;
using UnityEngine;

public class GameTimeSystem : MonoBehaviour
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int MinutesPerDay = HoursPerDay * MinutesPerHour;

    [Header("Start Time")]
    [Min(1)] [SerializeField] private int startDay = 1;
    [Range(0, 23)] [SerializeField] private int startHour = 8;
    [Range(0, 59)] [SerializeField] private int startMinute = 0;

    [Header("Time Flow")]
    [Min(0.1f)] [SerializeField] private float realMinutesPerGameDay = 20f;
    [Min(0f)] [SerializeField] private float timeScale = 1f;
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool useUnscaledTime;

    [Header("День/Ночь")]
    [Range(0, 23)] [SerializeField] private int dayStartHour = 6;
    [Range(0, 23)] [SerializeField] private int nightStartHour = 20;

    public event Action<int, int, int> OnMinuteChanged;
    public event Action<int> OnDayChanged;
    public event Action<bool> OnDayNightChanged;

    public int Day => currentDay;
    public int Hour => currentHour;
    public int Minute => currentMinute;
    public bool IsNight => isNight;
    public bool IsDay => !isNight;
    public bool IsRunning => isRunning;
    public float TimeScale => timeScale;
    public string TimeLabel => $"{currentHour:00}:{currentMinute:00}";
    public float TimeOfDay01 => (currentHour * MinutesPerHour + currentMinute) / (float)MinutesPerDay;
    public float TimeOfDayPrecise01
    {
        get
        {
            float minuteOfDay = currentHour * MinutesPerHour + currentMinute + Mathf.Clamp01(accumulatedGameMinutes);
            return Mathf.Repeat(minuteOfDay / MinutesPerDay, 1f);
        }
    }

    private int currentDay;
    private int currentHour;
    private int currentMinute;
    private bool isNight;
    private bool isRunning;
    private float accumulatedGameMinutes;

    private void Awake()
    {
        InitializeFromStartValues();
        isRunning = runOnStart;
    }

    private void Start()
    {
        NotifyCurrentState(includeDayNight: true, includeDay: true);
    }

    private void Update()
    {
        if (!isRunning || timeScale <= 0f)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        float realSecondsPerGameDay = Mathf.Max(0.01f, realMinutesPerGameDay * 60f);
        float gameMinutesPerSecond = MinutesPerDay / realSecondsPerGameDay;

        accumulatedGameMinutes += deltaTime * timeScale * gameMinutesPerSecond;
        int wholeMinutes = Mathf.FloorToInt(accumulatedGameMinutes);

        if (wholeMinutes <= 0)
            return;

        accumulatedGameMinutes -= wholeMinutes;
        AddMinutes(wholeMinutes);
    }

    public void SetRunning(bool value)
    {
        isRunning = value;
    }

    public void ToggleRunning()
    {
        isRunning = !isRunning;
    }

    public void SetTimeScale(float value)
    {
        timeScale = Mathf.Max(0f, value);
    }

    public void SetTime(int day, int hour, int minute, bool notify = true)
    {
        int previousDay = currentDay;
        bool previousNight = isNight;

        currentDay = Mathf.Max(1, day);
        currentHour = Mathf.Clamp(hour, 0, 23);
        currentMinute = Mathf.Clamp(minute, 0, 59);
        isNight = CalculateIsNight(currentHour);
        accumulatedGameMinutes = 0f;

        if (!notify)
            return;

        if (currentDay != previousDay)
            OnDayChanged?.Invoke(currentDay);

        if (isNight != previousNight)
            OnDayNightChanged?.Invoke(isNight);

        OnMinuteChanged?.Invoke(currentDay, currentHour, currentMinute);
    }

    public void AddMinutes(int minutes)
    {
        if (minutes <= 0)
            return;

        int previousDay = currentDay;
        bool previousNight = isNight;

        int totalMinutes = currentHour * MinutesPerHour + currentMinute + minutes;
        int dayIncrease = totalMinutes / MinutesPerDay;
        int minuteOfDay = totalMinutes % MinutesPerDay;

        currentDay += dayIncrease;
        currentHour = minuteOfDay / MinutesPerHour;
        currentMinute = minuteOfDay % MinutesPerHour;
        isNight = CalculateIsNight(currentHour);

        if (currentDay != previousDay)
            OnDayChanged?.Invoke(currentDay);

        if (isNight != previousNight)
            OnDayNightChanged?.Invoke(isNight);

        OnMinuteChanged?.Invoke(currentDay, currentHour, currentMinute);
    }

    public void NotifyCurrentState(bool includeDayNight = false, bool includeDay = false)
    {
        if (includeDay)
            OnDayChanged?.Invoke(currentDay);

        if (includeDayNight)
            OnDayNightChanged?.Invoke(isNight);

        OnMinuteChanged?.Invoke(currentDay, currentHour, currentMinute);
    }

    private void InitializeFromStartValues()
    {
        currentDay = Mathf.Max(1, startDay);
        currentHour = Mathf.Clamp(startHour, 0, 23);
        currentMinute = Mathf.Clamp(startMinute, 0, 59);
        isNight = CalculateIsNight(currentHour);
        accumulatedGameMinutes = 0f;
    }

    private bool CalculateIsNight(int hour)
    {
        return !IsDayHour(hour);
    }

    private bool IsDayHour(int hour)
    {
        if (dayStartHour == nightStartHour)
            return true;

        if (dayStartHour < nightStartHour)
            return hour >= dayStartHour && hour < nightStartHour;

        return hour >= dayStartHour || hour < nightStartHour;
    }

    private void OnValidate()
    {
        startDay = Mathf.Max(1, startDay);
        realMinutesPerGameDay = Mathf.Max(0.1f, realMinutesPerGameDay);
        timeScale = Mathf.Max(0f, timeScale);

        if (!Application.isPlaying)
            return;

        isNight = CalculateIsNight(currentHour);
    }
}
