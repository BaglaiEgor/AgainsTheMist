using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PotionEffectsUI : MonoBehaviour
{
    public enum EffectSlot
    {
        FogProtection,
        FrostProtection,
        HealCooldown
    }

    private sealed class EffectView
    {
        public EffectSlot slot;
        public GameObject root;
        public PotionEffectIconUI iconUi;
        public ItemData sourceItem;
        public float remainingTime;
        public string effectText;
        public string timerLabel;
        public bool active;
    }

    [Header("References")]
    [SerializeField] private PlayerPotionEffects playerPotionEffects;
    [SerializeField] private ItemTooltip sharedTooltip;
    [SerializeField] private Transform iconsRoot;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hidePanelWhenNoEffects = true;

    private readonly EffectView[] views = new EffectView[3];
    private EffectView hoveredView;

    void Awake()
    {
        EnsureReferences();
        CreateViewsIfNeeded();
        HideTooltip();
    }

    void Update()
    {
        EnsureReferences();
        UpdateViews();
        UpdateTooltip();
    }

    void EnsureReferences()
    {
        if (playerPotionEffects == null)
        {
            playerPotionEffects = GetComponent<PlayerPotionEffects>();
            if (playerPotionEffects == null)
                playerPotionEffects = FindFirstObjectByType<PlayerPotionEffects>();
        }

        if (sharedTooltip == null)
            sharedTooltip = FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include);

        if (iconsRoot == null)
            iconsRoot = transform;
    }

    void CreateViewsIfNeeded()
    {
        EnsureView(0, EffectSlot.FogProtection, "FogProtectionEffect");
        EnsureView(1, EffectSlot.FrostProtection, "FrostProtectionEffect");
        EnsureView(2, EffectSlot.HealCooldown, "HealingCooldownEffect");
    }

    void EnsureView(int index, EffectSlot slot, string fallbackName)
    {
        if (views[index] != null)
            return;

        GameObject viewObject;
        if (iconPrefab != null)
        {
            viewObject = Instantiate(iconPrefab, iconsRoot);
            viewObject.name = fallbackName;
        }
        else
        {
            viewObject = new GameObject(fallbackName, typeof(RectTransform), typeof(Image));
            viewObject.transform.SetParent(iconsRoot, false);
            RectTransform rectTransform = viewObject.transform as RectTransform;
            if (rectTransform != null)
                rectTransform.sizeDelta = new Vector2(56f, 56f);
        }

        PotionEffectIconUI iconUi = viewObject.GetComponent<PotionEffectIconUI>();
        if (iconUi == null)
            iconUi = viewObject.AddComponent<PotionEffectIconUI>();

        iconUi.Initialize(slot, OnEffectHoverEnter, OnEffectHoverExit);

        views[index] = new EffectView
        {
            slot = slot,
            root = viewObject,
            iconUi = iconUi
        };
    }

    void UpdateViews()
    {
        if (playerPotionEffects == null)
        {
            SetAllViewsInactive();
            return;
        }

        ItemData fogPotion = playerPotionEffects.ActiveFogProtectionPotion;
        ConfigureView(
            views[0],
            fogPotion != null && playerPotionEffects.RemainingFogEffectTime > 0f,
            fogPotion,
            playerPotionEffects.RemainingFogEffectTime,
            "Осталось",
            "Рост давления x" + playerPotionEffects.GetPressureGrowthMultiplier().ToString("0.##")
        );

        ItemData frostPotion = playerPotionEffects.ActiveFrostProtectionPotion;
        ConfigureView(
            views[1],
            frostPotion != null && playerPotionEffects.RemainingFrostEffectTime > 0f,
            frostPotion,
            playerPotionEffects.RemainingFrostEffectTime,
            "Осталось",
            "Морозный урон x" + playerPotionEffects.GetFrostDamageMultiplier().ToString("0.##")
        );

        ItemData healCooldownPotion = playerPotionEffects.ActiveHealingCooldownPotion;
        ConfigureView(
            views[2],
            healCooldownPotion != null && playerPotionEffects.RemainingHealCooldown > 0f,
            healCooldownPotion,
            playerPotionEffects.RemainingHealCooldown,
            "КД",
            "Зелье лечения на перезарядке"
        );

        bool anyActive = views[0].active || views[1].active || views[2].active;
        if (hidePanelWhenNoEffects && panelRoot != null)
            panelRoot.SetActive(anyActive);

        if (hoveredView != null && !hoveredView.active)
        {
            hoveredView = null;
            HideTooltip();
        }
    }

    void ConfigureView(EffectView view, bool active, ItemData item, float remainingTime, string timerLabel, string effectText)
    {
        if (view == null || view.root == null || view.iconUi == null)
            return;

        view.active = active;
        view.sourceItem = item;
        view.remainingTime = Mathf.Max(0f, remainingTime);
        view.timerLabel = timerLabel;
        view.effectText = effectText ?? string.Empty;

        view.root.SetActive(active);
        if (!active)
            return;

        int remainingSeconds = Mathf.CeilToInt(view.remainingTime);
        view.iconUi.SetData(item != null ? item.icon : null, remainingSeconds);
    }

    void SetAllViewsInactive()
    {
        for (int i = 0; i < views.Length; i++)
        {
            EffectView view = views[i];
            if (view == null || view.root == null)
                continue;

            view.active = false;
            view.root.SetActive(false);
        }

        if (hidePanelWhenNoEffects && panelRoot != null)
            panelRoot.SetActive(false);

        hoveredView = null;
        HideTooltip();
    }

    void OnEffectHoverEnter(EffectSlot slot)
    {
        hoveredView = GetViewBySlot(slot);
        if (hoveredView == null || !hoveredView.active)
        {
            hoveredView = null;
            HideTooltip();
            return;
        }

        ShowTooltipForHovered();
    }

    void OnEffectHoverExit(EffectSlot slot)
    {
        EffectView view = GetViewBySlot(slot);
        if (hoveredView != view)
            return;

        hoveredView = null;
        HideTooltip();
    }

    EffectView GetViewBySlot(EffectSlot slot)
    {
        for (int i = 0; i < views.Length; i++)
        {
            EffectView view = views[i];
            if (view != null && view.slot == slot)
                return view;
        }

        return null;
    }

    void UpdateTooltip()
    {
        if (hoveredView == null || !hoveredView.active)
            return;

        ShowTooltipForHovered();
    }

    void ShowTooltipForHovered()
    {
        if (sharedTooltip == null || hoveredView == null)
            return;

        ItemData item = hoveredView.sourceItem;
        string name = item != null ? item.itemName : "Эффект";
        string description = item != null ? item.description : string.Empty;
        int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, hoveredView.remainingTime));
        string extra = hoveredView.effectText + "\n" + hoveredView.timerLabel + ": " + remainingSeconds + " c";
        sharedTooltip.ShowCustomLeftBottom(name, description, extra);
    }

    void HideTooltip()
    {
        if (sharedTooltip != null)
            sharedTooltip.Hide();
    }
}
