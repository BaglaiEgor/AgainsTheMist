using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GuidanceSystem : MonoBehaviour
{
    public static GuidanceSystem Instance { get; private set; }
    public static event Action<GuidanceEntry, string> OnActiveGuidanceChanged;
    public static event Action<IReadOnlyList<GuidanceEntry>> OnUnlockedGuidanceChanged;

    [Header("Initial")]
    [SerializeField] private GuidanceEntry initialGuidance;
    [TextArea(2, 4)] [SerializeField] private string fallbackInitialText = "Осмотрись рядом с маяком и собери первые ресурсы.";

    [Header("Unlock Popup")]
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private GuidanceUnlockPopupUI unlockPopupPrefab;

    private GuidanceEntry activeEntry;
    private string activeText;
    private readonly List<GuidanceEntry> unlockedEntries = new();
    private readonly HashSet<string> unlockedIds = new();

    public GuidanceEntry ActiveEntry => activeEntry;
    public string ActiveText => activeText;
    public IReadOnlyList<GuidanceEntry> UnlockedEntries => unlockedEntries;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (initialGuidance != null)
            SetGuidance(initialGuidance);
        else if (!string.IsNullOrWhiteSpace(fallbackInitialText))
            SetGuidanceText(fallbackInitialText);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetGuidance(GuidanceEntry entry)
    {
        activeEntry = entry;
        activeText = entry != null ? entry.PlayerText : string.Empty;
        OnActiveGuidanceChanged?.Invoke(activeEntry, activeText);
    }

    public void SetGuidanceText(string text)
    {
        activeEntry = null;
        activeText = text ?? string.Empty;
        OnActiveGuidanceChanged?.Invoke(activeEntry, activeText);
    }

    public void ClearGuidance()
    {
        activeEntry = null;
        activeText = string.Empty;
        OnActiveGuidanceChanged?.Invoke(null, activeText);
    }

    public bool Unlock(GuidanceEntry entry)
    {
        return Unlock(entry, (Transform)null);
    }

    public bool Unlock(GuidanceEntry entry, Transform popupAnchor)
    {
        Vector3? popupPosition = popupAnchor != null ? popupAnchor.position : null;
        return Unlock(entry, popupPosition);
    }

    public bool Unlock(GuidanceEntry entry, Vector3? popupWorldPosition)
    {
        if (entry == null)
            return false;

        string key = string.IsNullOrWhiteSpace(entry.Id) ? entry.name : entry.Id;
        if (string.IsNullOrWhiteSpace(key) || unlockedIds.Contains(key))
            return false;

        unlockedIds.Add(key);
        unlockedEntries.Add(entry);
        OnUnlockedGuidanceChanged?.Invoke(unlockedEntries);

        if (popupWorldPosition.HasValue)
            ShowUnlockPopup(entry, popupWorldPosition.Value);

        return true;
    }

    public void Restore(IEnumerable<string> unlockedEntryIds, GuidanceEntry restoredActiveEntry, string restoredActiveText)
    {
        unlockedIds.Clear();
        unlockedEntries.Clear();

        if (unlockedEntryIds != null)
        {
            GuidanceEntry[] entries = Resources.LoadAll<GuidanceEntry>(string.Empty);
            foreach (string id in unlockedEntryIds)
            {
                GuidanceEntry entry = FindEntry(entries, id);
                if (entry == null)
                    continue;

                string key = string.IsNullOrWhiteSpace(entry.Id) ? entry.name : entry.Id;
                if (string.IsNullOrWhiteSpace(key) || !unlockedIds.Add(key))
                    continue;

                unlockedEntries.Add(entry);
            }
        }

        OnUnlockedGuidanceChanged?.Invoke(unlockedEntries);

        if (restoredActiveEntry != null)
            SetGuidance(restoredActiveEntry);
        else if (!string.IsNullOrWhiteSpace(restoredActiveText))
            SetGuidanceText(restoredActiveText);
        else
            ClearGuidance();
    }

    private static GuidanceEntry FindEntry(GuidanceEntry[] entries, string id)
    {
        if (entries == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            GuidanceEntry entry = entries[i];
            if (entry == null)
                continue;

            string key = string.IsNullOrWhiteSpace(entry.Id) ? entry.name : entry.Id;
            if (key == id)
                return entry;
        }

        return null;
    }

    private void ShowUnlockPopup(GuidanceEntry entry, Vector3 worldPosition)
    {
        if (unlockPopupPrefab == null)
            return;

        Canvas targetCanvas = popupCanvas != null ? popupCanvas : FindFirstObjectByType<Canvas>();
        if (targetCanvas == null)
            return;

        GuidanceUnlockPopupUI popup = Instantiate(unlockPopupPrefab, targetCanvas.transform);
        popup.Show(entry, worldPosition, targetCanvas);
    }
}
