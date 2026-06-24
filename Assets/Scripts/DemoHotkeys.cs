using UnityEngine;
using UnityEngine.InputSystem;

public class DemoHotkeys : MonoBehaviour
{
    private static DemoHotkeys instance;

    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private GameTimeSystem timeSystem;

    [Header("Give Amounts")]
    [Min(1)] [SerializeField] private int materialAmount = 40;
    [Min(1)] [SerializeField] private int seedAmount = 12;

    [Header("Overlay")]
    [SerializeField] private bool showOverlay = true;

    private string lastAction = "Демо-режим готов";

    private const float FastTimeScale = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject bootstrap = new GameObject("[DemoHotkeys]");
        bootstrap.AddComponent<DemoHotkeys>();
        DontDestroyOnLoad(bootstrap);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        ResolveReferences();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
            showOverlay = !showOverlay;

        if (keyboard.f2Key.wasPressedThisFrame)
            GiveMaterials();

        if (keyboard.f3Key.wasPressedThisFrame)
            GiveTools();

        if (keyboard.f4Key.wasPressedThisFrame)
            GiveBuildingSet();

        if (keyboard.f5Key.wasPressedThisFrame)
            GiveSeedSet();

        if (keyboard.f6Key.wasPressedThisFrame)
            SkipHalfDay();

        if (keyboard.f7Key.wasPressedThisFrame)
            ToggleTimeFlow();

        if (keyboard.f8Key.wasPressedThisFrame)
            SetMorning();

        if (keyboard.f9Key.wasPressedThisFrame)
            ToggleFastTime();

    }

    private void OnGUI()
    {
        if (!showOverlay)
            return;

        GUILayout.BeginArea(new Rect(12f, 12f, 420f, 285f), GUI.skin.box);
        GUILayout.Label("ДЕМО-ХОТКЕИ");
        GUILayout.Label("F2: +материалы (дерево/камень/шерсть/железо/золото/уголь/мастер-ключ)");
        GUILayout.Label("F3: +инструменты (топор/кирка/меч)");
        GUILayout.Label("F4: +набор строительства (печь/грядка/пол/стена)");
        GUILayout.Label("F5: +семена моркови");
        GUILayout.Label("F6: +12 игровых часов");
        GUILayout.Label("F7: пауза/продолжить время");
        GUILayout.Label("F8: установить 08:00");
        GUILayout.Label("F1: показать/скрыть подсказку");
        GUILayout.Label("F9: Time speed x5 / x1");
        GUILayout.Space(8f);
        GUILayout.Label("Последнее действие: " + lastAction);
        GUILayout.EndArea();
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<Inventory>();

        if (timeSystem == null)
            timeSystem = FindFirstObjectByType<GameTimeSystem>();
    }

    private void GiveMaterials()
    {
        if (!EnsureInventory())
            return;

        int added = 0;
        added += AddByPath("Materials/Wood", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/Stone", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/Wool", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/Wool", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/IronOre", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/IronIngot", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/GoldOre", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/GoldIngot", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/Coal", materialAmount) ? 1 : 0;
        added += AddByPath("Garden/MistHerb", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/FogEssence", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/FrostEssence", materialAmount) ? 1 : 0;
        added += AddByPath("Materials/Master key", 1) ? 1 : 0;
        lastAction = added > 0
            ? $"Материалы добавлены x{materialAmount}"
            : "Материалы не найдены в Resources";
    }

    private void GiveTools()
    {
        if (!EnsureInventory())
            return;

        int added = 0;
        added += AddByPath("Tools/AxeGold", 1) ? 1 : 0;
        added += AddByPath("Tools/PickaxeGold", 1) ? 1 : 0;
        added += AddByPath("Tools/HoeGold", 1) ? 1 : 0;
        added += AddByPath("Tools/ShovelGold", 1) ? 1 : 0;
        added += AddByPath("Weapons/SwordGold", 1) ? 1 : 0;

        lastAction = added > 0
            ? "Инструменты добавлены"
            : "Инструменты не найдены в Resources";
    }

    private void GiveBuildingSet()
    {
        if (!EnsureInventory())
            return;

        int added = 0;
        added += AddByPath("Structures/Objects/Furnace", 1) ? 1 : 0;
        added += AddByPath("Structures/Objects/GardenBed", 1) ? 1 : 0;
        added += AddByPath("Structures/Objects/Door", 5) ? 1 : 0;
        added += AddByPath("Structures/Tiles/WoodFloor", 30) ? 1 : 0;
        added += AddByPath("Structures/Tiles/WoodBridge", 30) ? 1 : 0;
        added += AddByPath("Structures/Tiles/WoodWall", 20) ? 1 : 0;

        lastAction = added > 0
            ? "Набор строительства добавлен"
            : "Предметы строительства не найдены в Resources";
    }

    private void GiveSeedSet()
    {
        if (!EnsureInventory())
            return;

        bool added = AddByPath("Garden/CarrotSeed", seedAmount);
        lastAction = added
            ? $"Семена добавлены x{seedAmount}"
            : "Семена не найдены в Resources";
    }

    private void SkipHalfDay()
    {
        if (!EnsureTimeSystem())
            return;

        timeSystem.AddMinutes(12 * 60);
        lastAction = "Время сдвинуто на 12 часов";
    }

    private void ToggleTimeFlow()
    {
        if (!EnsureTimeSystem())
            return;

        timeSystem.ToggleRunning();
        lastAction = timeSystem.IsRunning ? "Время запущено" : "Время на паузе";
    }

    private void SetMorning()
    {
        if (!EnsureTimeSystem())
            return;

        timeSystem.SetTime(timeSystem.Day, 8, 0);
        lastAction = $"Утро установлено: День {timeSystem.Day}, 08:00";
    }

    private void ToggleFastTime()
    {
        if (!EnsureTimeSystem())
            return;

        bool fastTimeEnabled = timeSystem.TimeScale >= FastTimeScale;
        timeSystem.SetTimeScale(fastTimeEnabled ? 1f : FastTimeScale);
        lastAction = fastTimeEnabled ? "Time speed: x1" : "Time speed: x5";
    }

    private bool AddByPath(string resourcePath, int amount)
    {
        ItemData item = Resources.Load<ItemData>(resourcePath);
        if (item == null)
            return false;

        return InventoryTransactionService.TryInsertItem(inventory, item, Mathf.Max(1, amount));
    }

    private bool EnsureInventory()
    {
        if (inventory != null)
            return true;

        lastAction = "Инвентарь не найден на сцене";
        return false;
    }

    private bool EnsureTimeSystem()
    {
        if (timeSystem != null)
            return true;

        lastAction = "Система времени не найдена на сцене";
        return false;
    }

}
