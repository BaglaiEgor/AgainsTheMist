using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ContentToolsWindow : EditorWindow
{
    private const string ItemDatabasePath = "Assets/Resources/Items/ItemDatabase.asset";

    private readonly Dictionary<Type, UnityEngine.Object[]> assetCache = new Dictionary<Type, UnityEngine.Object[]>();

    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private UnityEngine.Object selectedAsset;
    private Editor selectedEditor;

    private string itemSearch = "";
    private string recipeSearch = "";
    private string gardenSearch = "";
    private string lootSearch = "";
    private string questSearch = "";
    private string guidanceSearch = "";
    private string databaseSearch = "";

    private string newItemName = "NewItem";
    private string newRecipeName = "NewRecipe";
    private string newCropName = "CropDefinition";
    private string newLootName = "NewLootTable";
    private string newQuestName = "LighthouseKeeperQuest";
    private string newGuidanceName = "GuidanceEntry";
    private string newDatabaseName = "ItemDatabase";

    private ItemType newItemType = ItemType.Material;
    private string selectedCraftFolder = "Assets/Resources/Crafts";

    private bool showItems = true;
    private bool showRecipes = true;
    private bool showGarden = true;
    private bool showLoot = true;
    private bool showNpc = true;
    private bool showDatabases = true;

    private GUIStyle selectedRowStyle;
    private Texture2D selectedRowTexture;

    [MenuItem("Tools/Against the Mist/Content Tools")]
    public static void Open()
    {
        GetWindow<ContentToolsWindow>("Content Tools");
    }

    private void OnDisable()
    {
        DestroySelectedEditor();

        if (selectedRowTexture != null)
        {
            DestroyImmediate(selectedRowTexture);
            selectedRowTexture = null;
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Against the Mist Content Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                RefreshCache();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(320f), GUILayout.MaxWidth(420f)))
            {
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

                DrawItemsBlock();
                DrawRecipesBlock();
                DrawGardenBlock();
                DrawLootBlock();
                DrawNpcBlock();
                DrawDatabasesBlock();

                EditorGUILayout.EndScrollView();
            }

            Rect splitter = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(splitter, new Color(0.25f, 0.25f, 0.25f));

            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(560f)))
            {
                rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                DrawSelectedInspector();
                EditorGUILayout.EndScrollView();
            }
        }
    }

    private void DrawItemsBlock()
    {
        showItems = EditorGUILayout.Foldout(showItems, "Items", true, EditorStyles.foldoutHeader);
        if (!showItems)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            newItemType = (ItemType)EditorGUILayout.EnumPopup("Type", newItemType);
            DrawCreateRow("Name", ref newItemName, GetItemFolder(newItemType), CreateItemAsset);
            DrawAssetList<ItemData>(ref itemSearch);
        }
    }

    private void DrawRecipesBlock()
    {
        showRecipes = EditorGUILayout.Foldout(showRecipes, "Recipes", true, EditorStyles.foldoutHeader);
        if (!showRecipes)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string[] craftFolders = GetCraftFolders();
            int selectedIndex = Mathf.Max(0, Array.IndexOf(craftFolders, selectedCraftFolder));
            selectedIndex = EditorGUILayout.Popup("Folder", selectedIndex, craftFolders);
            selectedCraftFolder = craftFolders.Length > 0 ? craftFolders[selectedIndex] : "Assets/Resources/Crafts";

            DrawCreateRow("Name", ref newRecipeName, selectedCraftFolder, () => CreateAsset<CraftingRecipe>(newRecipeName, selectedCraftFolder));
            DrawAssetList<CraftingRecipe>(ref recipeSearch);
        }
    }

    private void DrawGardenBlock()
    {
        showGarden = EditorGUILayout.Foldout(showGarden, "Garden", true, EditorStyles.foldoutHeader);
        if (!showGarden)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawCreateRow("Crop Name", ref newCropName, "Assets/Resources/Garden", () => CreateAsset<CropDefinition>(newCropName, "Assets/Resources/Garden"));
            DrawAssetList<CropDefinition>(ref gardenSearch);
        }
    }

    private void DrawLootBlock()
    {
        showLoot = EditorGUILayout.Foldout(showLoot, "Loot", true, EditorStyles.foldoutHeader);
        if (!showLoot)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawCreateRow("Loot Name", ref newLootName, "Assets/Resources/LootTables", () => CreateAsset<LootTable>(newLootName, "Assets/Resources/LootTables"));
            DrawAssetList<LootTable>(ref lootSearch);
        }
    }

    private void DrawNpcBlock()
    {
        showNpc = EditorGUILayout.Foldout(showNpc, "NPC", true, EditorStyles.foldoutHeader);
        if (!showNpc)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Lighthouse Keeper Quests", EditorStyles.boldLabel);
            DrawCreateRow("Quest Name", ref newQuestName, "Assets/Resources/NPC/LighthouseKeeper/Quests", () => CreateAsset<LighthouseKeeperQuest>(newQuestName, "Assets/Resources/NPC/LighthouseKeeper/Quests"));
            DrawAssetList<LighthouseKeeperQuest>(ref questSearch);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Guidance Tips", EditorStyles.boldLabel);
            DrawCreateRow("Tip Name", ref newGuidanceName, "Assets/Resources/NPC/LighthouseKeeper/Tips", () => CreateAsset<GuidanceEntry>(newGuidanceName, "Assets/Resources/NPC/LighthouseKeeper/Tips"));
            DrawAssetList<GuidanceEntry>(ref guidanceSearch);
        }
    }

    private void DrawDatabasesBlock()
    {
        showDatabases = EditorGUILayout.Foldout(showDatabases, "Databases", true, EditorStyles.foldoutHeader);
        if (!showDatabases)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find ItemDatabase", GUILayout.Width(140f)))
                {
                    ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
                    if (database != null)
                        SelectAsset(database);
                    else
                        Debug.LogWarning("ItemDatabase not found at " + ItemDatabasePath);
                }

                if (GUILayout.Button("Rebuild ItemDatabase", GUILayout.Width(160f)))
                    RebuildItemDatabase();
            }

            DrawCreateRow("Database Name", ref newDatabaseName, "Assets/Resources/Items", () => CreateAsset<ItemDatabase>(newDatabaseName, "Assets/Resources/Items"));
            DrawAssetList<ItemDatabase>(ref databaseSearch);
        }
    }

    private void DrawCreateRow(string label, ref string assetName, string folder, Action createAction)
    {
        EditorGUILayout.LabelField("Create in: " + folder, EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            assetName = EditorGUILayout.TextField(label, assetName);

            GUI.enabled = !string.IsNullOrWhiteSpace(assetName);
            if (GUILayout.Button("Create", GUILayout.Width(80f)))
                createAction.Invoke();
            GUI.enabled = true;
        }
    }

    private void DrawAssetList<T>(ref string search) where T : ScriptableObject
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("Search", search);

            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                search = "";
        }

        UnityEngine.Object[] assets = GetAssets<T>();
        int shownCount = 0;

        for (int i = 0; i < assets.Length; i++)
        {
            T asset = assets[i] as T;
            if (asset == null || !MatchesSearch(asset.name, search))
                continue;

            shownCount++;
            DrawAssetRow(asset);
        }

        if (shownCount == 0)
            EditorGUILayout.HelpBox("Assets not found.", MessageType.Info);
    }

    private void DrawAssetRow(UnityEngine.Object asset)
    {
        bool isSelected = selectedAsset == asset;
        GUIStyle rowStyle = isSelected ? selectedRowStyle : GUIStyle.none;

        using (new EditorGUILayout.HorizontalScope(rowStyle))
        {
            if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.MinWidth(120f)))
                SelectAsset(asset);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select", GUILayout.Width(60f)))
                SelectAsset(asset);

            if (GUILayout.Button("Ping", GUILayout.Width(45f)))
                EditorGUIUtility.PingObject(asset);
        }
    }

    private void DrawLinkedPrefabs(ItemData item)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Linked Prefabs", EditorStyles.boldLabel);

        DrawPrefabRow("pickupPrefab", item.pickupPrefab);
        DrawPrefabRow("prefab", item.prefab);
        DrawPrefabRow("attackPrefab", item.attackPrefab);
    }

    private void DrawPrefabRow(string label, GameObject prefab)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(label, prefab, typeof(GameObject), false);

            GUI.enabled = prefab != null;

            if (GUILayout.Button("Select", GUILayout.Width(60f)))
                Selection.activeObject = prefab;

            if (GUILayout.Button("Ping", GUILayout.Width(45f)))
                EditorGUIUtility.PingObject(prefab);

            if (GUILayout.Button("Open Prefab", GUILayout.Width(90f)))
                AssetDatabase.OpenAsset(prefab);

            GUI.enabled = true;
        }
    }

    private void DrawSelectedInspector()
    {
        EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

        if (selectedAsset == null)
        {
            EditorGUILayout.HelpBox("Select any asset from the left panel.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string path = AssetDatabase.GetAssetPath(selectedAsset);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(selectedAsset, selectedAsset.GetType(), false);

                if (GUILayout.Button("Ping", GUILayout.Width(45f)))
                    EditorGUIUtility.PingObject(selectedAsset);

                if (GUILayout.Button("Open", GUILayout.Width(50f)))
                    AssetDatabase.OpenAsset(selectedAsset);
            }

            EditorGUILayout.SelectableLabel(path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        ItemData item = selectedAsset as ItemData;
        if (item != null)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawLinkedPrefabs(item);
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (selectedEditor == null || selectedEditor.target != selectedAsset)
            {
                DestroySelectedEditor();
                selectedEditor = Editor.CreateEditor(selectedAsset);
            }

            if (selectedEditor != null)
            {
                EditorGUI.BeginChangeCheck();
                selectedEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(selectedAsset);
                    RefreshCache();
                }
            }
        }
    }

    private void SelectAsset(UnityEngine.Object asset)
    {
        selectedAsset = asset;
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        DestroySelectedEditor();
        Repaint();
    }

    private void CreateAsset<T>(string assetName, string folder) where T : ScriptableObject
    {
        EnsureFolder(folder);

        T asset = CreateInstance<T>();
        string safeName = string.IsNullOrWhiteSpace(assetName) ? typeof(T).Name : assetName.Trim();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + ".asset");

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshCache();
        SelectAsset(asset);
    }

    private void CreateItemAsset()
    {
        string folder = GetItemFolder(newItemType);
        EnsureFolder(folder);

        ItemData asset = CreateInstance<ItemData>();
        asset.type = newItemType;
        asset.itemID = newItemName.Trim();

        string safeName = string.IsNullOrWhiteSpace(newItemName) ? typeof(ItemData).Name : newItemName.Trim();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeName + ".asset");

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RefreshCache();
        SelectAsset(asset);
    }

    private UnityEngine.Object[] GetAssets<T>() where T : ScriptableObject
    {
        Type type = typeof(T);

        UnityEngine.Object[] cachedAssets;
        if (assetCache.TryGetValue(type, out cachedAssets))
            return cachedAssets;

        string[] guids = AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets/Resources" });
        List<UnityEngine.Object> assets = new List<UnityEngine.Object>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                assets.Add(asset);
        }

        assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        cachedAssets = assets.ToArray();
        assetCache[type] = cachedAssets;
        return cachedAssets;
    }

    private void RebuildItemDatabase()
    {
        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database == null)
        {
            Debug.LogWarning("ItemDatabase not found at " + ItemDatabasePath);
            return;
        }

        UnityEngine.Object[] itemAssets = GetAssets<ItemData>();
        SerializedObject serializedDatabase = new SerializedObject(database);
        SerializedProperty itemsProperty = serializedDatabase.FindProperty("items");

        if (itemsProperty == null)
        {
            Debug.LogWarning("Could not find ItemDatabase items list.");
            return;
        }

        itemsProperty.ClearArray();
        for (int i = 0; i < itemAssets.Length; i++)
        {
            itemsProperty.InsertArrayElementAtIndex(i);
            itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = itemAssets[i];
        }

        serializedDatabase.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SelectAsset(database);

        Debug.Log("ItemDatabase rebuilt. Items count: " + itemAssets.Length);
    }

    private void RefreshCache()
    {
        assetCache.Clear();
        Repaint();
    }

    private void EnsureStyles()
    {
        if (selectedRowStyle != null)
            return;

        selectedRowTexture = new Texture2D(1, 1);
        selectedRowTexture.hideFlags = HideFlags.HideAndDontSave;
        selectedRowTexture.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.36f, 0.55f) : new Color(0.55f, 0.72f, 0.95f));
        selectedRowTexture.Apply();

        selectedRowStyle = new GUIStyle(EditorStyles.helpBox);
        selectedRowStyle.normal.background = selectedRowTexture;
        selectedRowStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    private static bool MatchesSearch(string assetName, string search)
    {
        return string.IsNullOrWhiteSpace(search) ||
               assetName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetItemFolder(ItemType type)
    {
        switch (type)
        {
            case ItemType.Tool:
                return "Assets/Resources/Tools";
            case ItemType.Weapon:
                return "Assets/Resources/Weapons";
            case ItemType.Structure:
                return "Assets/Resources/Structures/Objects";
            case ItemType.Seed:
            case ItemType.Food:
                return "Assets/Resources/Garden";
            case ItemType.Lantern:
                return "Assets/Resources/Tools";
            case ItemType.Equipment:
                return "Assets/Resources/Equipment";
            case ItemType.Consumable:
                return "Assets/Resources/Consumables";
            default:
                return "Assets/Resources/Materials";
        }
    }

    private static string[] GetCraftFolders()
    {
        List<string> folders = new List<string> { "Assets/Resources/Crafts" };

        if (AssetDatabase.IsValidFolder("Assets/Resources/Crafts"))
        {
            string[] subFolders = AssetDatabase.GetSubFolders("Assets/Resources/Crafts");
            for (int i = 0; i < subFolders.Length; i++)
                folders.Add(subFolders[i]);
        }

        return folders.ToArray();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private void DestroySelectedEditor()
    {
        if (selectedEditor == null)
            return;

        DestroyImmediate(selectedEditor);
        selectedEditor = null;
    }
}
