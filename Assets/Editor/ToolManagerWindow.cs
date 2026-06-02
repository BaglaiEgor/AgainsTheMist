using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ToolManagerWindow : EditorWindow
{
    private const string DefaultItemDatabasePath = "Assets/Resources/Items/ItemDatabase.asset";

    private enum ToolPage
    {
        ContentTools,
        ItemDatabaseBuilder,
        CraftRecipeBuilder,
        PickupPrefabGenerator,
        SpriteBrowser
    }

    [Serializable]
    private class IngredientRow
    {
        public ItemData item;
        public int amount = 1;
    }

    private ToolPage currentPage = ToolPage.ItemDatabaseBuilder;
    private Vector2 navigationScroll;
    private Vector2 contentScroll;

    private ItemDatabase itemDatabase;
    private string itemSearchRoot = "Assets";
    private readonly List<ItemData> foundItems = new();
    private readonly List<ItemData> missingDatabaseItems = new();
    private readonly List<ItemData> emptyIdItems = new();
    private readonly List<string> duplicateItemIds = new();

    private string recipeFolder = "Assets/Resources/Crafts";
    private string recipeName = "";
    private ItemData recipeResult;
    private int recipeResultAmount = 1;
    private CraftStationType recipeStation = CraftStationType.None;
    private CraftingCategory recipeCategory = CraftingCategory.Basic;
    private bool addRecipeToOpenSceneManagers = true;
    private readonly List<IngredientRow> recipeIngredients = new();

    private GameObject pickupTemplatePrefab;
    private string pickupOutputFolder = "Assets/Prefabs/Pickups";
    private string pickupPrefix = "Pickup_";
    private string pickupItemSearch = "";
    private bool pickupOnlyMissing = true;
    private bool pickupUseItemIcon = true;
    private bool pickupAssignToItemData = true;
    private bool pickupOverwriteExisting;
    private int pickupAmount = 1;

    private ItemData spriteTargetItem;
    private string spriteSearchFolder = "Assets/Sprites";
    private string spriteNameFilter = "";
    private float spritePreviewSize = 96f;
    private readonly List<Sprite> foundSprites = new();

    [MenuItem("Tools/Against the Mist/Tool Manager")]
    public static void Open()
    {
        GetWindow<ToolManagerWindow>("Tool Manager");
    }

    private void OnEnable()
    {
        if (itemDatabase == null)
            itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DefaultItemDatabasePath);

        if (recipeIngredients.Count == 0)
            recipeIngredients.Add(new IngredientRow());

        RefreshItemDatabaseState();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawNavigation();

            Rect splitter = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(splitter, new Color(0.25f, 0.25f, 0.25f));

            using (new EditorGUILayout.VerticalScope())
            {
                contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
                DrawCurrentPage();
                EditorGUILayout.EndScrollView();
            }
        }
    }

    private void DrawNavigation()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(220f)))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Against the Mist", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tool Manager", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(6f);

            navigationScroll = EditorGUILayout.BeginScrollView(navigationScroll);
            DrawNavigationButton(ToolPage.ContentTools, "Content Tools");
            DrawNavigationButton(ToolPage.ItemDatabaseBuilder, "Item Database Builder");
            DrawNavigationButton(ToolPage.CraftRecipeBuilder, "Craft Recipe Builder");
            DrawNavigationButton(ToolPage.PickupPrefabGenerator, "Pickup Prefab Generator");
            DrawNavigationButton(ToolPage.SpriteBrowser, "Sprite Browser");
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawNavigationButton(ToolPage page, string label)
    {
        GUIStyle style = currentPage == page ? EditorStyles.toolbarButton : EditorStyles.miniButton;
        if (GUILayout.Button(label, style, GUILayout.Height(30f)))
        {
            currentPage = page;
            contentScroll = Vector2.zero;
        }
    }

    private void DrawCurrentPage()
    {
        EditorGUILayout.Space(6f);

        switch (currentPage)
        {
            case ToolPage.ContentTools:
                DrawContentToolsPage();
                break;
            case ToolPage.ItemDatabaseBuilder:
                DrawItemDatabaseBuilder();
                break;
            case ToolPage.CraftRecipeBuilder:
                DrawCraftRecipeBuilder();
                break;
            case ToolPage.PickupPrefabGenerator:
                DrawPickupPrefabGenerator();
                break;
            case ToolPage.SpriteBrowser:
                DrawSpriteBrowser();
                break;
        }
    }

    private void DrawContentToolsPage()
    {
        EditorGUILayout.LabelField("Content Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Старое окно оставлено как контент-браузер: создание и быстрый инспектор ItemData, рецептов, лута, NPC и баз.", MessageType.Info);

        if (GUILayout.Button("Open Existing Content Tools", GUILayout.Width(220f)))
            ContentToolsWindow.Open();
    }

    private void DrawItemDatabaseBuilder()
    {
        EditorGUILayout.LabelField("Item Database Builder", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            itemDatabase = (ItemDatabase)EditorGUILayout.ObjectField("Item Database", itemDatabase, typeof(ItemDatabase), false);
            itemSearchRoot = EditorGUILayout.TextField("Search Root", itemSearchRoot);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
                    RefreshItemDatabaseState();

                GUI.enabled = itemDatabase != null && missingDatabaseItems.Count > 0;
                if (GUILayout.Button("Add Missing", GUILayout.Width(120f)))
                    AddMissingItemsToDatabase();

                GUI.enabled = itemDatabase != null && foundItems.Count > 0;
                if (GUILayout.Button("Rebuild All", GUILayout.Width(120f)))
                    RebuildItemDatabase();
                GUI.enabled = true;
            }
        }

        EditorGUILayout.LabelField($"Found ItemData: {foundItems.Count}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Missing in database: {missingDatabaseItems.Count}");
        EditorGUILayout.LabelField($"Empty itemID: {emptyIdItems.Count}");
        EditorGUILayout.LabelField($"Duplicate itemID: {duplicateItemIds.Count}");

        DrawItemList("Missing Items", missingDatabaseItems);
        DrawItemList("Empty ID Items", emptyIdItems);
        DrawStringList("Duplicate IDs", duplicateItemIds);
    }

    private void DrawCraftRecipeBuilder()
    {
        EditorGUILayout.LabelField("Craft Recipe Builder", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            recipeFolder = EditorGUILayout.TextField("Folder", recipeFolder);
            recipeResult = (ItemData)EditorGUILayout.ObjectField("Result", recipeResult, typeof(ItemData), false);
            recipeResultAmount = Mathf.Max(1, EditorGUILayout.IntField("Result Amount", recipeResultAmount));
            recipeStation = (CraftStationType)EditorGUILayout.EnumPopup("Station", recipeStation);
            recipeCategory = (CraftingCategory)EditorGUILayout.EnumPopup("Category", recipeCategory);
            addRecipeToOpenSceneManagers = EditorGUILayout.Toggle("Add To Open Scene Managers", addRecipeToOpenSceneManagers);

            string fallbackName = recipeResult != null ? "Recipe_" + recipeResult.name : "NewRecipe";
            if (string.IsNullOrWhiteSpace(recipeName) || recipeName == "NewRecipe")
                recipeName = fallbackName;
            recipeName = EditorGUILayout.TextField("Asset Name", recipeName);
        }

        EditorGUILayout.LabelField("Ingredients", EditorStyles.boldLabel);
        for (int i = 0; i < recipeIngredients.Count; i++)
        {
            IngredientRow row = recipeIngredients[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                row.item = (ItemData)EditorGUILayout.ObjectField(row.item, typeof(ItemData), false);
                row.amount = Mathf.Max(1, EditorGUILayout.IntField(row.amount, GUILayout.Width(60f)));

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    recipeIngredients.RemoveAt(i);
                    i--;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Ingredient", GUILayout.Width(130f)))
                recipeIngredients.Add(new IngredientRow());

            GUI.enabled = recipeResult != null && !string.IsNullOrWhiteSpace(recipeName);
            if (GUILayout.Button("Create Recipe", GUILayout.Width(130f)))
                CreateRecipeAsset();
            GUI.enabled = true;
        }
    }

    private void DrawPickupPrefabGenerator()
    {
        EditorGUILayout.LabelField("Pickup Prefab Generator", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            pickupTemplatePrefab = (GameObject)EditorGUILayout.ObjectField("Template Prefab", pickupTemplatePrefab, typeof(GameObject), false);
            pickupOutputFolder = EditorGUILayout.TextField("Output Folder", pickupOutputFolder);
            pickupPrefix = EditorGUILayout.TextField("Prefab Prefix", pickupPrefix);
            pickupItemSearch = EditorGUILayout.TextField("Item Name Filter", pickupItemSearch);
            pickupAmount = Mathf.Max(1, EditorGUILayout.IntField("Pickup Amount", pickupAmount));
            pickupOnlyMissing = EditorGUILayout.Toggle("Only Missing pickupPrefab", pickupOnlyMissing);
            pickupUseItemIcon = EditorGUILayout.Toggle("Use item.icon Sprite", pickupUseItemIcon);
            pickupAssignToItemData = EditorGUILayout.Toggle("Assign To ItemData", pickupAssignToItemData);
            pickupOverwriteExisting = EditorGUILayout.Toggle("Overwrite Existing Prefab Asset", pickupOverwriteExisting);

            GUI.enabled = pickupTemplatePrefab != null;
            if (GUILayout.Button("Generate Pickups", GUILayout.Width(160f)))
                GeneratePickupPrefabs();
            GUI.enabled = true;
        }

        EditorGUILayout.HelpBox("Генератор копирует выбранный рабочий prefab, меняет PickupItem.itemData/amount, SpriteRenderer.sprite и при необходимости прописывает prefab обратно в ItemData.pickupPrefab.", MessageType.Info);
    }

    private void DrawSpriteBrowser()
    {
        EditorGUILayout.LabelField("Sprite Browser", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            spriteTargetItem = (ItemData)EditorGUILayout.ObjectField("Target ItemData", spriteTargetItem, typeof(ItemData), false);
            spriteSearchFolder = EditorGUILayout.TextField("Sprite Folder", spriteSearchFolder);
            spriteNameFilter = EditorGUILayout.TextField("Name Filter", spriteNameFilter);
            spritePreviewSize = EditorGUILayout.Slider("Preview Size", spritePreviewSize, 48f, 192f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Sprites", GUILayout.Width(130f)))
                    RefreshSpriteBrowser();

                GUI.enabled = spriteTargetItem != null && spriteTargetItem.icon != null;
                if (GUILayout.Button("Ping Current Icon", GUILayout.Width(130f)))
                    EditorGUIUtility.PingObject(spriteTargetItem.icon);
                GUI.enabled = true;
            }
        }

        if (spriteTargetItem == null)
            EditorGUILayout.HelpBox("Выбери ItemData, потом кликни по большому превью спрайта ниже. Спрайт сразу запишется в ItemData.icon.", MessageType.Info);

        if (foundSprites.Count == 0)
            RefreshSpriteBrowser();

        DrawSpriteGrid();
    }

    private void RefreshItemDatabaseState()
    {
        foundItems.Clear();
        missingDatabaseItems.Clear();
        emptyIdItems.Clear();
        duplicateItemIds.Clear();

        string[] searchFolders = GetExistingSearchFolders(itemSearchRoot);
        string[] guids = AssetDatabase.FindAssets("t:ItemData", searchFolders);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null && !foundItems.Contains(item))
                foundItems.Add(item);
        }

        foundItems.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        BuildItemValidationLists();
    }

    private void BuildItemValidationLists()
    {
        HashSet<ItemData> databaseItems = new();
        if (itemDatabase != null)
        {
            SerializedObject serializedDatabase = new SerializedObject(itemDatabase);
            SerializedProperty itemsProperty = serializedDatabase.FindProperty("items");
            if (itemsProperty != null)
            {
                for (int i = 0; i < itemsProperty.arraySize; i++)
                {
                    ItemData item = itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
                    if (item != null)
                        databaseItems.Add(item);
                }
            }
        }

        Dictionary<string, int> idCounts = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < foundItems.Count; i++)
        {
            ItemData item = foundItems[i];
            if (!databaseItems.Contains(item))
                missingDatabaseItems.Add(item);

            if (string.IsNullOrWhiteSpace(item.itemID))
            {
                emptyIdItems.Add(item);
                continue;
            }

            idCounts.TryGetValue(item.itemID, out int count);
            idCounts[item.itemID] = count + 1;
        }

        foreach (KeyValuePair<string, int> pair in idCounts)
        {
            if (pair.Value > 1)
                duplicateItemIds.Add(pair.Key);
        }
    }

    private void AddMissingItemsToDatabase()
    {
        if (itemDatabase == null)
            return;

        SerializedObject serializedDatabase = new SerializedObject(itemDatabase);
        SerializedProperty itemsProperty = serializedDatabase.FindProperty("items");
        if (itemsProperty == null)
            return;

        int added = 0;
        for (int i = 0; i < missingDatabaseItems.Count; i++)
        {
            ItemData item = missingDatabaseItems[i];
            if (item == null)
                continue;

            itemsProperty.InsertArrayElementAtIndex(itemsProperty.arraySize);
            itemsProperty.GetArrayElementAtIndex(itemsProperty.arraySize - 1).objectReferenceValue = item;
            added++;
        }

        serializedDatabase.ApplyModifiedProperties();
        EditorUtility.SetDirty(itemDatabase);
        AssetDatabase.SaveAssets();
        RefreshItemDatabaseState();
        Debug.Log("ItemDatabase Builder: added missing items: " + added);
    }

    private void RebuildItemDatabase()
    {
        if (itemDatabase == null)
            return;

        SerializedObject serializedDatabase = new SerializedObject(itemDatabase);
        SerializedProperty itemsProperty = serializedDatabase.FindProperty("items");
        if (itemsProperty == null)
            return;

        itemsProperty.ClearArray();
        for (int i = 0; i < foundItems.Count; i++)
        {
            itemsProperty.InsertArrayElementAtIndex(i);
            itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue = foundItems[i];
        }

        serializedDatabase.ApplyModifiedProperties();
        EditorUtility.SetDirty(itemDatabase);
        AssetDatabase.SaveAssets();
        RefreshItemDatabaseState();
        Debug.Log("ItemDatabase Builder: rebuilt database. Items count: " + foundItems.Count);
    }

    private void CreateRecipeAsset()
    {
        EnsureFolder(recipeFolder);

        CraftingRecipe recipe = CreateInstance<CraftingRecipe>();
        recipe.result = recipeResult;
        recipe.resultAmount = Mathf.Max(1, recipeResultAmount);
        recipe.station = recipeStation;
        recipe.category = recipeCategory;

        List<CraftingRecipe.Ingredient> validIngredients = new();
        for (int i = 0; i < recipeIngredients.Count; i++)
        {
            IngredientRow row = recipeIngredients[i];
            if (row == null || row.item == null)
                continue;

            validIngredients.Add(new CraftingRecipe.Ingredient
            {
                item = row.item,
                amount = Mathf.Max(1, row.amount)
            });
        }

        recipe.ingredients = validIngredients.ToArray();

        string path = AssetDatabase.GenerateUniqueAssetPath(recipeFolder + "/" + SanitizeFileName(recipeName) + ".asset");
        AssetDatabase.CreateAsset(recipe, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (addRecipeToOpenSceneManagers)
            AddRecipeToOpenSceneManagers(recipe);

        Selection.activeObject = recipe;
        EditorGUIUtility.PingObject(recipe);
        Debug.Log("Craft Recipe Builder: created " + path);
    }

    private void AddRecipeToOpenSceneManagers(CraftingRecipe recipe)
    {
        CraftingManager[] managers = FindObjectsByType<CraftingManager>(FindObjectsSortMode.None);
        int changed = 0;

        for (int i = 0; i < managers.Length; i++)
        {
            CraftingManager manager = managers[i];
            if (manager == null)
                continue;

            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty recipesProperty = serializedManager.FindProperty("recipes");
            if (recipesProperty == null || ContainsObjectReference(recipesProperty, recipe))
                continue;

            recipesProperty.InsertArrayElementAtIndex(recipesProperty.arraySize);
            recipesProperty.GetArrayElementAtIndex(recipesProperty.arraySize - 1).objectReferenceValue = recipe;
            serializedManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            changed++;
        }

        if (changed > 0)
            Debug.Log("Craft Recipe Builder: added recipe to open scene CraftingManager count: " + changed);
    }

    private void GeneratePickupPrefabs()
    {
        string templatePath = AssetDatabase.GetAssetPath(pickupTemplatePrefab);
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            Debug.LogWarning("Pickup Prefab Generator: template prefab must be a project asset.");
            return;
        }

        EnsureFolder(pickupOutputFolder);
        RefreshItemDatabaseState();

        int created = 0;
        int updated = 0;
        int skipped = 0;
        int missingIcon = 0;
        int missingPickupComponent = 0;

        for (int i = 0; i < foundItems.Count; i++)
        {
            ItemData item = foundItems[i];
            if (item == null)
                continue;

            if (pickupOnlyMissing && item.pickupPrefab != null)
            {
                skipped++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(pickupItemSearch) &&
                item.name.IndexOf(pickupItemSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                (string.IsNullOrWhiteSpace(item.itemName) || item.itemName.IndexOf(pickupItemSearch, StringComparison.OrdinalIgnoreCase) < 0))
            {
                skipped++;
                continue;
            }

            if (pickupUseItemIcon && item.icon == null)
                missingIcon++;

            string prefabName = pickupPrefix + item.name;
            string prefabPath = pickupOutputFolder + "/" + SanitizeFileName(prefabName) + ".prefab";
            bool exists = File.Exists(prefabPath);

            if (exists && !pickupOverwriteExisting)
            {
                skipped++;
                continue;
            }

            if (!exists)
            {
                if (!AssetDatabase.CopyAsset(templatePath, prefabPath))
                {
                    Debug.LogWarning("Pickup Prefab Generator: failed to copy prefab to " + prefabPath);
                    skipped++;
                    continue;
                }

                created++;
            }
            else
            {
                updated++;
            }

            if (!ConfigurePickupPrefab(prefabPath, item, ref missingPickupComponent))
            {
                skipped++;
                continue;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pickupAssignToItemData && prefabAsset != null)
            {
                Undo.RecordObject(item, "Assign pickup prefab");
                item.pickupPrefab = prefabAsset;
                EditorUtility.SetDirty(item);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshItemDatabaseState();

        Debug.Log($"Pickup Prefab Generator: created {created}, updated {updated}, skipped {skipped}, missing icon {missingIcon}, missing pickup component {missingPickupComponent}");
    }

    private void RefreshSpriteBrowser()
    {
        foundSprites.Clear();

        string[] searchFolders = GetExistingSearchFolders(spriteSearchFolder);
        string[] guids = AssetDatabase.FindAssets("t:Sprite", searchFolders);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int j = 0; j < assets.Length; j++)
            {
                Sprite sprite = assets[j] as Sprite;
                if (sprite == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(spriteNameFilter) &&
                    sprite.name.IndexOf(spriteNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!foundSprites.Contains(sprite))
                    foundSprites.Add(sprite);
            }
        }

        foundSprites.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private void DrawSpriteGrid()
    {
        EditorGUILayout.LabelField($"Sprites: {foundSprites.Count}", EditorStyles.boldLabel);

        float tileSize = Mathf.Clamp(spritePreviewSize, 48f, 192f);
        float viewWidth = Mathf.Max(260f, position.width - 260f);
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (tileSize + 18f)));
        int index = 0;

        while (index < foundSprites.Count)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int column = 0; column < columns && index < foundSprites.Count; column++, index++)
                    DrawSpriteTile(foundSprites[index], tileSize);
            }
        }
    }

    private void DrawSpriteTile(Sprite sprite, float tileSize)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(tileSize + 12f)))
        {
            Rect previewRect = GUILayoutUtility.GetRect(tileSize, tileSize, GUILayout.Width(tileSize), GUILayout.Height(tileSize));
            DrawSpritePreview(previewRect, sprite);

            if (GUI.Button(previewRect, GUIContent.none, GUIStyle.none))
                AssignSpriteToTarget(sprite);

            string label = sprite != null ? sprite.name : "None";
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(tileSize), GUILayout.Height(32f));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = spriteTargetItem != null;
                if (GUILayout.Button("Set", GUILayout.Width((tileSize - 4f) * 0.5f)))
                    AssignSpriteToTarget(sprite);
                GUI.enabled = true;

                if (GUILayout.Button("Ping", GUILayout.Width((tileSize - 4f) * 0.5f)))
                    EditorGUIUtility.PingObject(sprite);
            }
        }
    }

    private void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            return;
        }

        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height
        );

        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);

        if (spriteTargetItem != null && spriteTargetItem.icon == sprite)
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Color.green);
    }

    private void AssignSpriteToTarget(Sprite sprite)
    {
        if (spriteTargetItem == null || sprite == null)
            return;

        Undo.RecordObject(spriteTargetItem, "Assign item icon");
        spriteTargetItem.icon = sprite;
        EditorUtility.SetDirty(spriteTargetItem);
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private bool ConfigurePickupPrefab(string prefabPath, ItemData item, ref int missingPickupComponent)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
            return false;

        bool result = true;
        try
        {
            root.name = Path.GetFileNameWithoutExtension(prefabPath);

            PickupItem pickup = root.GetComponentInChildren<PickupItem>(true);
            if (pickup == null)
            {
                pickup = root.AddComponent<PickupItem>();
                missingPickupComponent++;
            }

            pickup.itemData = item;
            pickup.amount = Mathf.Max(1, pickupAmount);
            EditorUtility.SetDirty(pickup);

            if (pickupUseItemIcon && item.icon != null)
            {
                SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.sprite = item.icon;
                    EditorUtility.SetDirty(renderer);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            result = false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return result;
    }

    private void DrawItemList(string title, List<ItemData> items)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(item, typeof(ItemData), false);
                if (GUILayout.Button("Ping", GUILayout.Width(45f)))
                    EditorGUIUtility.PingObject(item);
            }
        }
    }

    private void DrawStringList(string title, List<string> values)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (values.Count == 0)
        {
            EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < values.Count; i++)
            EditorGUILayout.SelectableLabel(values[i], GUILayout.Height(EditorGUIUtility.singleLineHeight));
    }

    private static bool ContainsObjectReference(SerializedProperty arrayProperty, UnityEngine.Object target)
    {
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return true;
        }

        return false;
    }

    private static string[] GetExistingSearchFolders(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return new[] { "Assets" };

        string normalized = root.Trim().Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
            return new[] { normalized };

        return new[] { "Assets" };
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Replace("\\", "/").Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "NewAsset" : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            safe = safe.Replace(invalid[i], '_');

        return safe;
    }
}
