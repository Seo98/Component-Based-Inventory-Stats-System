using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ItemDatabase에 등록된 ItemData 에셋을 일괄 관리하는 에디터 창입니다.
/// </summary>
public class ItemDatabaseEditor : EditorWindow
{
    private const string DefaultSearchRoot = "Assets/@Scripts/System/GridInventory/DataSO";

    private enum ViewSortMode
    {
        DatabaseOrder,
        IdAscending,
        TypeThenId
    }

    private ItemDatabase _database;
    private DefaultAsset _searchRoot;
    private Vector2 _scrollPosition;
    private ViewSortMode _sortMode = ViewSortMode.TypeThenId;
    private ItemType _typeFilter = ItemType.Any;
    private bool _showDuplicatesOnly;

    /// <summary>
    /// Item Database Editor 창을 엽니다.
    /// </summary>
    [MenuItem("Tools/Item Database Editor")]
    public static void ShowWindow()
    {
        GetWindow<ItemDatabaseEditor>("Item DB Editor").minSize = new Vector2(1280, 540);
    }

    private void OnGUI()
    {
        GUILayout.Space(8);

        _database = (ItemDatabase)EditorGUILayout.ObjectField("Item Database", _database, typeof(ItemDatabase), false);
        _searchRoot = (DefaultAsset)EditorGUILayout.ObjectField("Search Root", _searchRoot, typeof(DefaultAsset), false);

        if (_database == null)
        {
            EditorGUILayout.HelpBox("ItemDatabase 에셋을 할당해주세요.", MessageType.Info);
            return;
        }

        SerializedObject dbSO = new SerializedObject(_database);
        SerializedProperty allItemsProp = dbSO.FindProperty("_allItems");

        if (allItemsProp == null)
        {
            EditorGUILayout.HelpBox("_allItems 필드를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        DrawToolbar(dbSO, allItemsProp);

        HashSet<int> duplicateIds = GetDuplicateIds(allItemsProp);
        if (duplicateIds.Count > 0)
            EditorGUILayout.HelpBox($"중복 ItemID {duplicateIds.Count}개 감지: {string.Join(", ", duplicateIds)}", MessageType.Warning);

        DrawHeader();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        ItemType? lastType = null;
        foreach (int index in BuildVisibleIndices(allItemsProp, duplicateIds))
        {
            ItemData itemData = allItemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemData;

            if (_sortMode == ViewSortMode.TypeThenId && itemData != null && lastType != itemData.Type)
            {
                lastType = itemData.Type;
                EditorGUILayout.LabelField(itemData.Type.ToString(), EditorStyles.boldLabel);
            }

            DrawItemRow(allItemsProp, index, duplicateIds);
        }

        EditorGUILayout.EndScrollView();

        if (dbSO.ApplyModifiedProperties())
            EditorUtility.SetDirty(_database);
    }

    private void DrawToolbar(SerializedObject dbSO, SerializedProperty allItemsProp)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add All ItemData", GUILayout.Width(130)))
            AddAllItemDataAssets(dbSO, allItemsProp);

        if (GUILayout.Button("Remove Nulls", GUILayout.Width(100)))
            RemoveNullEntries(dbSO, allItemsProp);

        if (GUILayout.Button("Sort DB By ID", GUILayout.Width(110)))
            SortDatabaseById(dbSO, allItemsProp);

        if (GUILayout.Button("Save Assets", GUILayout.Width(100)))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        GUILayout.Space(12);
        GUILayout.Label($"Count: {allItemsProp.arraySize}", EditorStyles.boldLabel, GUILayout.Width(90));

        _sortMode = (ViewSortMode)EditorGUILayout.EnumPopup("View", _sortMode, GUILayout.Width(220));
        _typeFilter = (ItemType)EditorGUILayout.EnumPopup("Type", _typeFilter, GUILayout.Width(190));
        _showDuplicatesOnly = EditorGUILayout.ToggleLeft("Duplicates Only", _showDuplicatesOnly, GUILayout.Width(125));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("SO Asset", GUILayout.Width(150));
        GUILayout.Label("ID", GUILayout.Width(65));
        GUILayout.Label("Name", GUILayout.Width(130));
        GUILayout.Label("Rank", GUILayout.Width(80));
        GUILayout.Label("Type", GUILayout.Width(85));
        GUILayout.Label("Slot", GUILayout.Width(80));
        GUILayout.Label("Use", GUILayout.Width(105));
        GUILayout.Label("Stack", GUILayout.Width(55));
        GUILayout.Label("Weight", GUILayout.Width(60));
        GUILayout.Label("Buy", GUILayout.Width(55));
        GUILayout.Label("Sell", GUILayout.Width(55));
        GUILayout.Label("Hold", GUILayout.Width(45));
        GUILayout.Label("Hand", GUILayout.Width(90));
        GUILayout.Label("Dur", GUILayout.Width(55));
        GUILayout.Label("Req ID", GUILayout.Width(55));
        GUILayout.Label("Req Int", GUILayout.Width(60));
        GUILayout.Label("Corr", GUILayout.Width(55));
        GUILayout.Label("Sprite", GUILayout.Width(70));

        GUILayout.Space(12);

        GUILayout.Label("Modifiers", GUILayout.Width(80));
        GUILayout.Label("", GUILayout.Width(55));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawItemRow(SerializedProperty allItemsProp, int index, HashSet<int> duplicateIds)
    {
        SerializedProperty itemProp = allItemsProp.GetArrayElementAtIndex(index);
        ItemData itemData = itemProp.objectReferenceValue as ItemData;

        Color previousColor = GUI.backgroundColor;
        if (itemData != null && duplicateIds.Contains(itemData.ItemID))
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);

        EditorGUILayout.BeginHorizontal(GUI.skin.box);
        GUI.backgroundColor = previousColor;

        EditorGUILayout.PropertyField(itemProp, GUIContent.none, GUILayout.Width(150));

        if (itemData == null)
        {
            GUILayout.Label("Missing ItemData", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            return;
        }

        SerializedObject itemSO = new SerializedObject(itemData);
        itemSO.Update();

        DrawProperty(itemSO, "_itemID", 65);
        DrawProperty(itemSO, "_itemName", 130);
        DrawProperty(itemSO, "_rank", 80);
        DrawProperty(itemSO, "_type", 85);
        DrawProperty(itemSO, "_equipSlot", 80);
        DrawProperty(itemSO, "_useMechanic", 105);
        DrawProperty(itemSO, "_maxStackCount", 55);
        DrawProperty(itemSO, "_weight", 60);
        DrawProperty(itemSO, "_buyGold", 55);
        DrawProperty(itemSO, "_sellGold", 55);
        DrawProperty(itemSO, "_canHoldInHand", 45);
        DrawProperty(itemSO, "_handheldType", 90);
        DrawProperty(itemSO, "_maxDurability", 55);
        DrawProperty(itemSO, "_requiresIdentification", 55);
        DrawProperty(itemSO, "_requiredIntelligence", 60);
        DrawProperty(itemSO, "_correctionValue", 55);
        DrawProperty(itemSO, "_sprite", 70);

        GUILayout.Space(12);

        SerializedProperty modifiersProp = itemSO.FindProperty("_statModifiers");
        int modifierCount = modifiersProp != null ? modifiersProp.arraySize : 0;

        if (GUILayout.Button(modifierCount.ToString(), GUILayout.Width(80)))
        {
            Selection.activeObject = itemData;
            EditorGUIUtility.PingObject(itemData);
        }

        if (GUILayout.Button("Select", GUILayout.Width(55)))
        {
            Selection.activeObject = itemData;
            EditorGUIUtility.PingObject(itemData);
        }

        if (itemSO.ApplyModifiedProperties())
            EditorUtility.SetDirty(itemData);

        EditorGUILayout.EndHorizontal();
    }

    private List<int> BuildVisibleIndices(SerializedProperty allItemsProp, HashSet<int> duplicateIds)
    {
        List<int> indices = Enumerable.Range(0, allItemsProp.arraySize).ToList();

        indices = indices.Where(index =>
        {
            ItemData item = allItemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemData;
            if (item == null)
                return !_showDuplicatesOnly;

            if (_typeFilter != ItemType.Any && item.Type != _typeFilter)
                return false;

            if (_showDuplicatesOnly && !duplicateIds.Contains(item.ItemID))
                return false;

            return true;
        }).ToList();

        return _sortMode switch
        {
            ViewSortMode.IdAscending => indices.OrderBy(GetItemId).ThenBy(GetItemName).ToList(),
            ViewSortMode.TypeThenId => indices.OrderBy(GetItemType).ThenBy(GetItemId).ThenBy(GetItemName).ToList(),
            _ => indices
        };

        int GetItemId(int index) => (allItemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemData)?.ItemID ?? int.MaxValue;
        string GetItemName(int index) => (allItemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemData)?.Name ?? string.Empty;
        ItemType GetItemType(int index) => (allItemsProp.GetArrayElementAtIndex(index).objectReferenceValue as ItemData)?.Type ?? ItemType.Any;
    }

    private void AddAllItemDataAssets(SerializedObject dbSO, SerializedProperty allItemsProp)
    {
        string rootPath = GetSearchRootPath();

        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder", $"유효하지 않은 폴더입니다:\n{rootPath}", "OK");
            return;
        }

        HashSet<ItemData> existingItems = new HashSet<ItemData>();

        for (int i = 0; i < allItemsProp.arraySize; i++)
        {
            ItemData existing = allItemsProp.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
            if (existing != null)
                existingItems.Add(existing);
        }

        string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { rootPath });
        int addedCount = 0;

        Undo.RecordObject(_database, "Add All ItemData Assets");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (item == null || existingItems.Contains(item))
                continue;

            int newIndex = allItemsProp.arraySize;
            allItemsProp.InsertArrayElementAtIndex(newIndex);
            allItemsProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = item;

            existingItems.Add(item);
            addedCount++;
        }

        dbSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(_database);

        Debug.Log($"[ItemDatabaseEditor] Added {addedCount} ItemData assets from {rootPath}.");
    }

    private void RemoveNullEntries(SerializedObject dbSO, SerializedProperty allItemsProp)
    {
        Undo.RecordObject(_database, "Remove Null ItemData Entries");

        for (int i = allItemsProp.arraySize - 1; i >= 0; i--)
        {
            if (allItemsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                allItemsProp.DeleteArrayElementAtIndex(i);
        }

        dbSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(_database);
    }

    private void SortDatabaseById(SerializedObject dbSO, SerializedProperty allItemsProp)
    {
        List<ItemData> items = new List<ItemData>();

        for (int i = 0; i < allItemsProp.arraySize; i++)
        {
            ItemData item = allItemsProp.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
            if (item != null)
                items.Add(item);
        }

        items = items.OrderBy(item => item.ItemID).ThenBy(item => item.Name).ToList();

        Undo.RecordObject(_database, "Sort ItemDatabase By ID");

        allItemsProp.ClearArray();

        for (int i = 0; i < items.Count; i++)
        {
            allItemsProp.InsertArrayElementAtIndex(i);
            allItemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        dbSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(_database);
    }

    private HashSet<int> GetDuplicateIds(SerializedProperty allItemsProp)
    {
        Dictionary<int, int> idCounts = new Dictionary<int, int>();

        for (int i = 0; i < allItemsProp.arraySize; i++)
        {
            ItemData item = allItemsProp.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
            if (item == null)
                continue;

            if (!idCounts.TryAdd(item.ItemID, 1))
                idCounts[item.ItemID]++;
        }

        return idCounts.Where(pair => pair.Value > 1).Select(pair => pair.Key).ToHashSet();
    }

    private string GetSearchRootPath()
    {
        if (_searchRoot == null)
            return DefaultSearchRoot;

        string selectedPath = AssetDatabase.GetAssetPath(_searchRoot);
        return AssetDatabase.IsValidFolder(selectedPath) ? selectedPath : DefaultSearchRoot;
    }

    private void DrawProperty(SerializedObject so, string propertyName, float width)
    {
        SerializedProperty prop = so.FindProperty(propertyName);

        if (prop == null)
        {
            GUILayout.Label("-", GUILayout.Width(width));
            return;
        }

        EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(width));
    }

}
