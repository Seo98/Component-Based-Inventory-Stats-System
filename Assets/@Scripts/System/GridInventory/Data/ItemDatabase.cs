using System;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> _allItems = new List<ItemData>();

    private static ItemDatabase _instance;

    private Dictionary<int, ItemData> _itemDict;

    /// <summary>아이템 데이터베이스가 준비됐거나 다시 만들어졌을 때 발생합니다.</summary>
    public static event Action Initialized;

    /// <summary>런타임 아이템 조회가 가능한 상태인지 확인합니다.</summary>
    public static bool IsInitialized =>
        _instance != null && _instance._itemDict != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        Initialized = null;
    }

    public static void Initialize(ItemDatabase dbAsset)
    {
        if (dbAsset == null)
        {
            Debug.LogError("[ItemDatabase] 초기화할 데이터베이스가 null입니다.");
            return;
        }

        _instance = dbAsset;

        _instance._itemDict = new Dictionary<int, ItemData>();
        foreach (var item in _instance._allItems)
        {
            if (item != null && !_instance._itemDict.ContainsKey(item.ItemID))
            {
                _instance._itemDict.Add(item.ItemID, item);
            }
        }

        Debug.Log($"[ItemDatabase] 초기화 완료. 아이템 개수: {_instance._allItems.Count}");
        Initialized?.Invoke();
    }

    public static ItemData GetItem(int id)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[ItemDatabase] 초기화되지 않았습니다! Initialize()를 먼저 호출하세요.");
            return null;
        }

        return TryGetItem(id, out ItemData data) ? data : null;
    }

    /// <summary>
    /// 로그를 발생시키지 않고 ItemData를 조회합니다.
    /// 네트워크 검증이나 초기화 순서가 유동적인 시스템에서 사용합니다.
    /// </summary>
    public static bool TryGetItem(int id, out ItemData data)
    {
        data = null;

        if (!IsInitialized)
            return false;

        return _instance._itemDict.TryGetValue(id, out data);
    }
}
