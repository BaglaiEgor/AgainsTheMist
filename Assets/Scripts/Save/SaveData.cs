using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    public int version = 1;
    public string sceneName = "GameScene";
    public SaveVector3 playerPosition;
    public int playerHealth;
    public int playerMaxHealth;
    public float playerHunger;
    public int playerMaxHunger;
    public int day;
    public int hour;
    public int minute;
    public int activeInventorySlot;
    public List<SaveItemStack> inventory = new();
    public List<SaveItemStack> equipment = new();
    public List<string> unlockedGuidanceIds = new();
    public string activeGuidanceId;
    public string activeGuidanceText;
    public int beaconLevel;
    public float beaconRadius;
    public List<SaveContainerData> chests = new();
    public List<SaveFurnaceData> furnaces = new();
    public List<SaveGardenBedData> gardenBeds = new();
    public List<SavePlacedObjectData> placedObjects = new();
    public List<SavePlacedLanternData> placedLanterns = new();
    public List<SavePlacedTileData> placedTiles = new();
}

[Serializable]
public struct SaveVector3
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public struct SaveVector3Int
{
    public int x;
    public int y;
    public int z;
}

[Serializable]
public class SaveItemStack
{
    public string itemId;
    public int amount;
}

[Serializable]
public class SaveContainerData
{
    public string key;
    public SaveVector3 position;
    public List<SaveItemStack> slots = new();
}

[Serializable]
public class SaveFurnaceData : SaveContainerData
{
    public float fuelTimeRemaining;
    public float fuelTimeTotal;
    public float smeltProgress;
}

[Serializable]
public class SaveGardenBedData
{
    public SaveVector3 position;
    public string cropId;
    public int state;
    public int growthMinutes;
}

[Serializable]
public class SavePlacedObjectData
{
    public string itemId;
    public SaveVector3 position;
    public float rotationZ;
    public int rotationSteps;
}

[Serializable]
public class SavePlacedLanternData
{
    public string itemId;
    public SaveVector3 position;
    public float rotationZ;
    public float currentCharge;
    public float maxCharge;
    public float drainPerSecond;
}

[Serializable]
public class SavePlacedTileData
{
    public string itemId;
    public string tilemapName;
    public SaveVector3Int cell;
}
