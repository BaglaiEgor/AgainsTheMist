using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class SaveManager : MonoBehaviour
{
    private const string SaveFileName = "save.json";
    private const string MainMenuSceneName = "MainMenu";
    private const string GameSceneName = "GameScene";

    private static SaveManager instance;
    private static bool loadGameOnNextGameScene;

    [Header("Autosave")]
    [SerializeField] private float autosaveInterval = 60f;

    private float autosaveTimer;
    private ItemDatabase itemDatabase;
    private readonly Dictionary<string, ItemData> itemsById = new();
    private readonly Dictionary<string, GuidanceEntry> guidanceById = new();
    private readonly Dictionary<string, CropDefinition> cropsById = new();

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    public static bool HasSave() => File.Exists(SavePath);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static SaveManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        SaveManager existing = FindFirstObjectByType<SaveManager>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return instance;
        }

        GameObject go = new GameObject("[SaveManager]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SaveManager>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BuildCaches();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            return;

        if (autosaveInterval <= 0f)
            return;

        autosaveTimer += Time.unscaledDeltaTime;
        if (autosaveTimer < autosaveInterval)
            return;

        autosaveTimer = 0f;
        SaveGame();
    }

    private void OnApplicationQuit()
    {
        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            SaveGame();
    }

    public static void ContinueGame()
    {
        loadGameOnNextGameScene = true;
        SceneManager.LoadScene(GameSceneName);
    }

    public static void StartNewGame()
    {
        DeleteSave();
        loadGameOnNextGameScene = false;
        SceneManager.LoadScene(GameSceneName);
    }

    public static void SaveAndExitToMenu()
    {
        SaveGame();
        loadGameOnNextGameScene = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public static void SaveGame()
    {
        EnsureInstance().SaveGameInternal();
    }

    public static void LoadGame()
    {
        EnsureInstance().StartCoroutine(EnsureInstance().LoadGameRoutine());
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BuildCaches();
        SaveablePlacedTileRegistry.Clear();

        if (scene.name == GameSceneName && loadGameOnNextGameScene)
            StartCoroutine(LoadGameRoutine());
    }

    private void SaveGameInternal()
    {
        if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            return;

        BuildCaches();
        GameSaveData data = CreateSaveData();
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Game saved: {SavePath}");
    }

    private IEnumerator LoadGameRoutine()
    {
        loadGameOnNextGameScene = false;
        yield return null;

        if (!File.Exists(SavePath))
            yield break;

        BuildCaches();
        string json = File.ReadAllText(SavePath);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
        if (data == null)
            yield break;

        PrepareSceneForLoad();
        yield return null;

        ApplySaveData(data);
        Debug.Log($"Game loaded: {SavePath}");
    }

    private GameSaveData CreateSaveData()
    {
        GameSaveData data = new GameSaveData
        {
            sceneName = SceneManager.GetActiveScene().name
        };

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            data.playerPosition = ToSaveVector3(player.transform.position);

        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
        {
            data.playerHealth = health.CurrentHealth;
            data.playerMaxHealth = health.MaxHealth;
        }

        PlayerVitals vitals = FindFirstObjectByType<PlayerVitals>();
        if (vitals != null)
        {
            data.playerHunger = vitals.CurrentHunger;
            data.playerMaxHunger = vitals.MaxHunger;
        }

        GameTimeSystem time = FindFirstObjectByType<GameTimeSystem>();
        if (time != null)
        {
            data.day = time.Day;
            data.hour = time.Hour;
            data.minute = time.Minute;
        }

        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory != null)
        {
            data.activeInventorySlot = inventory.ActiveSlotIndex;
            data.inventory = CaptureContainerSlots(inventory);
            if (inventory.Equipment != null)
                data.equipment = CaptureContainerSlots(inventory.Equipment);
        }

        GuidanceSystem guidance = GuidanceSystem.Instance;
        if (guidance != null)
        {
            for (int i = 0; i < guidance.UnlockedEntries.Count; i++)
            {
                string id = GetGuidanceId(guidance.UnlockedEntries[i]);
                if (!string.IsNullOrWhiteSpace(id))
                    data.unlockedGuidanceIds.Add(id);
            }

            data.activeGuidanceId = GetGuidanceId(guidance.ActiveEntry);
            data.activeGuidanceText = guidance.ActiveText;
        }

        BeaconUpgrade beacon = FindFirstObjectByType<BeaconUpgrade>();
        if (beacon != null)
        {
            BeaconUpgradeSnapshot snapshot = beacon.CreateSnapshot();
            data.beaconLevel = snapshot.level;
            data.beaconRadius = snapshot.radius;
        }

        DonationFountain fountain = FindFirstObjectByType<DonationFountain>();
        if (fountain != null)
        {
            DonationFountainSnapshot snapshot = fountain.CreateSnapshot();
            data.donationFountainLevel = snapshot.level;
            data.donationFountainCoins = snapshot.coinsInCurrentLevel;
        }

        foreach (ChestInventory chest in FindObjectsByType<ChestInventory>(FindObjectsSortMode.None))
        {
            data.chests.Add(new SaveContainerData
            {
                key = BuildObjectKey(chest.transform),
                position = ToSaveVector3(chest.transform.position),
                slots = chest.CreateSlotSnapshot()
            });
        }

        foreach (FurnaceStation furnace in FindObjectsByType<FurnaceStation>(FindObjectsSortMode.None))
        {
            SaveFurnaceData furnaceData = furnace.CreateSnapshot();
            furnaceData.key = BuildObjectKey(furnace.transform);
            furnaceData.position = ToSaveVector3(furnace.transform.position);
            data.furnaces.Add(furnaceData);
        }

        foreach (GardenBed bed in FindObjectsByType<GardenBed>(FindObjectsSortMode.None))
            data.gardenBeds.Add(bed.CreateSnapshot());

        foreach (SaveablePlacedObject placed in FindObjectsByType<SaveablePlacedObject>(FindObjectsSortMode.None))
        {
            if (placed == null || placed.SourceItem == null || placed.GetComponent<PlacedLantern>() != null)
                continue;

            data.placedObjects.Add(new SavePlacedObjectData
            {
                itemId = GetItemId(placed.SourceItem),
                position = ToSaveVector3(placed.transform.position),
                rotationZ = placed.transform.eulerAngles.z,
                rotationSteps = placed.RotationSteps
            });
        }

        foreach (PlacedLantern lantern in FindObjectsByType<PlacedLantern>(FindObjectsSortMode.None))
            data.placedLanterns.Add(lantern.CreateSnapshot());

        foreach (SavePlacedTileData tile in SaveablePlacedTileRegistry.Tiles)
            data.placedTiles.Add(tile);

        return data;
    }

    private void ApplySaveData(GameSaveData data)
    {
        RestorePlacedObjects(data.placedObjects);
        RestoreGardenBeds(data.gardenBeds);
        RestorePlacedLanterns(data.placedLanterns);
        RestorePlacedTiles(data.placedTiles);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 position = ToVector3(data.playerPosition);
            player.transform.position = position;
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector2.zero;
            }
        }

        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null)
            health.Restore(data.playerHealth, data.playerMaxHealth);

        PlayerVitals vitals = FindFirstObjectByType<PlayerVitals>();
        if (vitals != null)
            vitals.RestoreHunger(data.playerHunger);

        GameTimeSystem time = FindFirstObjectByType<GameTimeSystem>();
        if (time != null && data.day > 0)
            time.SetTime(data.day, data.hour, data.minute);

        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory != null)
        {
            RestoreContainerSlots(inventory, data.inventory);
            inventory.SetActiveSlot(data.activeInventorySlot);

            if (inventory.Equipment != null)
                RestoreContainerSlots(inventory.Equipment, data.equipment);
        }

        GuidanceSystem guidance = GuidanceSystem.Instance;
        if (guidance != null)
            guidance.Restore(data.unlockedGuidanceIds, ResolveGuidance(data.activeGuidanceId), data.activeGuidanceText);

        BeaconUpgrade beacon = FindFirstObjectByType<BeaconUpgrade>();
        if (beacon != null)
            beacon.RestoreSnapshot(new BeaconUpgradeSnapshot(data.beaconLevel, data.beaconRadius));

        DonationFountain fountain = FindFirstObjectByType<DonationFountain>();
        if (fountain != null)
            fountain.RestoreSnapshot(new DonationFountainSnapshot(data.donationFountainLevel, data.donationFountainCoins));

        RestoreChests(data.chests);
        RestoreFurnaces(data.furnaces);
    }

    private void RestorePlacedObjects(List<SavePlacedObjectData> placedObjects)
    {
        if (placedObjects == null)
            return;

        for (int i = 0; i < placedObjects.Count; i++)
        {
            SavePlacedObjectData data = placedObjects[i];
            ItemData item = ResolveItem(data.itemId);
            if (item == null || item.prefab == null)
                continue;

            GameObject go = Instantiate(item.prefab, ToVector3(data.position), Quaternion.Euler(0f, 0f, data.rotationZ));
            SaveablePlacedObject marker = go.GetComponent<SaveablePlacedObject>();
            if (marker == null)
                marker = go.AddComponent<SaveablePlacedObject>();
            marker.Initialize(item, data.rotationSteps);

            WorldObjectOccupier occupier = go.GetComponent<WorldObjectOccupier>();
            if (occupier != null)
                occupier.SetPlacementRotationSteps(data.rotationSteps);

            Door door = go.GetComponent<Door>();
            if (door != null)
            {
                if (data.rotationSteps == 0)
                    door.Initialize(Vector3Int.left, Vector3Int.right);
                else
                    door.Initialize(Vector3Int.up, Vector3Int.down);
            }
        }
    }

    private void RestoreGardenBeds(List<SaveGardenBedData> gardenBeds)
    {
        if (gardenBeds == null)
            return;

        ItemData gardenBedItem = ResolveItem("GardenBed");
        GameObject prefab = gardenBedItem != null ? gardenBedItem.prefab : null;
        if (prefab == null)
            return;

        for (int i = 0; i < gardenBeds.Count; i++)
        {
            SaveGardenBedData data = gardenBeds[i];
            GameObject go = Instantiate(prefab, ToVector3(data.position), Quaternion.identity);
            GardenBed bed = go.GetComponent<GardenBed>();
            if (bed != null)
                bed.RestoreSnapshot(data, ResolveCrop(data.cropId));
        }
    }

    private void RestorePlacedLanterns(List<SavePlacedLanternData> lanterns)
    {
        if (lanterns == null)
            return;

        for (int i = 0; i < lanterns.Count; i++)
        {
            SavePlacedLanternData data = lanterns[i];
            ItemData item = ResolveItem(data.itemId);
            if (item == null || item.prefab == null)
                continue;

            GameObject go = Instantiate(item.prefab, ToVector3(data.position), Quaternion.Euler(0f, 0f, data.rotationZ));
            PlacedLantern lantern = go.GetComponent<PlacedLantern>();
            if (lantern == null)
                lantern = go.AddComponent<PlacedLantern>();

            lantern.RestoreSnapshot(data, item);

            SaveablePlacedObject marker = go.GetComponent<SaveablePlacedObject>();
            if (marker == null)
                marker = go.AddComponent<SaveablePlacedObject>();
            marker.Initialize(item, 0);
        }
    }

    private void RestorePlacedTiles(List<SavePlacedTileData> placedTiles)
    {
        SaveablePlacedTileRegistry.Restore(placedTiles);
        if (placedTiles == null)
            return;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        for (int i = 0; i < placedTiles.Count; i++)
        {
            SavePlacedTileData data = placedTiles[i];
            ItemData item = ResolveItem(data.itemId);
            Tilemap tilemap = FindTilemap(tilemaps, data.tilemapName);
            if (item == null || tilemap == null)
                continue;

            Vector3Int cell = ToVector3Int(data.cell);
            TileBase tile = GetTileForItem(item, data.tilemapName);
            if (tile == null)
                continue;

            tilemap.SetTile(cell, tile);
            tilemap.RefreshTile(cell);
            tilemap.RefreshTile(cell + Vector3Int.left);
            tilemap.RefreshTile(cell + Vector3Int.right);
            tilemap.RefreshTile(cell + Vector3Int.up);
            tilemap.RefreshTile(cell + Vector3Int.down);
            if (item.canPlaceOnWater)
                WorldGrid.RegisterPlacedBridge(cell);
            else if (item.occupiesBuildCell || item.isDoor)
                WorldGrid.RegisterPlacedTile(cell);
        }
    }

    private void RestoreChests(List<SaveContainerData> chests)
    {
        if (chests == null)
            return;

        ChestInventory[] sceneChests = FindObjectsByType<ChestInventory>(FindObjectsSortMode.None);
        for (int i = 0; i < chests.Count; i++)
        {
            ChestInventory chest = FindByKeyOrPosition(sceneChests, chests[i].key, ToVector3(chests[i].position));
            if (chest != null)
                chest.RestoreSlotSnapshot(chests[i].slots, ResolveItem);
        }
    }

    private void RestoreFurnaces(List<SaveFurnaceData> furnaces)
    {
        if (furnaces == null)
            return;

        FurnaceStation[] sceneFurnaces = FindObjectsByType<FurnaceStation>(FindObjectsSortMode.None);
        for (int i = 0; i < furnaces.Count; i++)
        {
            FurnaceStation furnace = FindByKeyOrPosition(sceneFurnaces, furnaces[i].key, ToVector3(furnaces[i].position));
            if (furnace != null)
                furnace.RestoreSnapshot(furnaces[i], ResolveItem);
        }
    }

    private void PrepareSceneForLoad()
    {
        foreach (SaveablePlacedObject placed in FindObjectsByType<SaveablePlacedObject>(FindObjectsSortMode.None))
        {
            if (placed != null)
                Destroy(placed.gameObject);
        }

        foreach (GardenBed bed in FindObjectsByType<GardenBed>(FindObjectsSortMode.None))
        {
            if (bed != null)
                Destroy(bed.gameObject);
        }
    }

    private List<SaveItemStack> CaptureContainerSlots(IItemContainer container)
    {
        List<SaveItemStack> result = new();
        if (container == null)
            return result;

        for (int i = 0; i < container.SlotCount; i++)
        {
            InventoryItem slot = container.GetItem(i);
            result.Add(ToStack(slot));
        }

        return result;
    }

    private void RestoreContainerSlots(IItemContainer container, List<SaveItemStack> slots)
    {
        if (container == null)
            return;

        for (int i = 0; i < container.SlotCount; i++)
        {
            SaveItemStack stack = slots != null && i < slots.Count ? slots[i] : null;
            ItemData item = stack != null ? ResolveItem(stack.itemId) : null;
            int amount = stack != null ? stack.amount : 0;
            container.SetSlotFromTransaction(i, item, amount);
        }
    }

    private SaveItemStack ToStack(InventoryItem slot)
    {
        if (slot == null || slot.IsEmpty || slot.item == null)
            return new SaveItemStack();

        return new SaveItemStack
        {
            itemId = GetItemId(slot.item),
            amount = slot.amount
        };
    }

    private void BuildCaches()
    {
        itemsById.Clear();
        guidanceById.Clear();
        cropsById.Clear();

        itemDatabase = Resources.Load<ItemDatabase>("Items/ItemDatabase");
        if (itemDatabase != null)
        {
            foreach (ItemData item in itemDatabase.AllItems)
                RegisterItem(item);
        }

        foreach (ItemData item in Resources.LoadAll<ItemData>(string.Empty))
            RegisterItem(item);

        foreach (GuidanceEntry entry in Resources.LoadAll<GuidanceEntry>(string.Empty))
        {
            string id = GetGuidanceId(entry);
            if (!string.IsNullOrWhiteSpace(id) && !guidanceById.ContainsKey(id))
                guidanceById.Add(id, entry);
        }

        foreach (CropDefinition crop in Resources.LoadAll<CropDefinition>(string.Empty))
        {
            if (crop != null && !string.IsNullOrWhiteSpace(crop.CropId) && !cropsById.ContainsKey(crop.CropId))
                cropsById.Add(crop.CropId, crop);
        }
    }

    private void RegisterItem(ItemData item)
    {
        string id = GetItemId(item);
        if (!string.IsNullOrWhiteSpace(id) && !itemsById.ContainsKey(id))
            itemsById.Add(id, item);
    }

    private ItemData ResolveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        if (itemDatabase != null)
        {
            ItemData fromDb = itemDatabase.FindById(itemId);
            if (fromDb != null)
                return fromDb;
        }

        if (itemsById.TryGetValue(itemId, out ItemData item))
            return item;

        return null;
    }

    private GuidanceEntry ResolveGuidance(string guidanceId)
    {
        if (string.IsNullOrWhiteSpace(guidanceId))
            return null;

        guidanceById.TryGetValue(guidanceId, out GuidanceEntry entry);
        return entry;
    }

    private CropDefinition ResolveCrop(string cropId)
    {
        if (string.IsNullOrWhiteSpace(cropId))
            return null;

        cropsById.TryGetValue(cropId, out CropDefinition crop);
        return crop;
    }

    public static string GetItemId(ItemData item)
    {
        if (item == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(item.itemID) ? item.name : item.itemID;
    }

    private static string GetGuidanceId(GuidanceEntry entry)
    {
        if (entry == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(entry.Id) ? entry.name : entry.Id;
    }

    private static string BuildObjectKey(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        Vector3 pos = transform.position;
        return $"{transform.name}:{pos.x:0.###}:{pos.y:0.###}:{pos.z:0.###}";
    }

    private static T FindByKeyOrPosition<T>(T[] objects, string key, Vector3 position) where T : Component
    {
        if (objects == null)
            return null;

        for (int i = 0; i < objects.Length; i++)
        {
            T obj = objects[i];
            if (obj != null && BuildObjectKey(obj.transform) == key)
                return obj;
        }

        T nearest = null;
        float bestDistance = 0.35f;
        for (int i = 0; i < objects.Length; i++)
        {
            T obj = objects[i];
            if (obj == null)
                continue;

            float distance = Vector3.Distance(obj.transform.position, position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = obj;
            }
        }

        return nearest;
    }

    private static Tilemap FindTilemap(Tilemap[] tilemaps, string tilemapName)
    {
        if (tilemaps == null || string.IsNullOrWhiteSpace(tilemapName))
            return null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            if (tilemaps[i] != null && tilemaps[i].name == tilemapName)
                return tilemaps[i];
        }

        return null;
    }

    private static TileBase GetTileForItem(ItemData item, string tilemapName)
    {
        if (item == null)
            return null;

        if (item.canPlaceOnWater)
            return item.waterTileToPlace;

        if (item.isDoor)
            return item.doorWallMarkerTile;

        return item.tileToPlace;
    }

    public static SaveVector3 ToSaveVector3(Vector3 value)
    {
        return new SaveVector3 { x = value.x, y = value.y, z = value.z };
    }

    public static Vector3 ToVector3(SaveVector3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    public static SaveVector3Int ToSaveVector3Int(Vector3Int value)
    {
        return new SaveVector3Int { x = value.x, y = value.y, z = value.z };
    }

    public static Vector3Int ToVector3Int(SaveVector3Int value)
    {
        return new Vector3Int(value.x, value.y, value.z);
    }
}
