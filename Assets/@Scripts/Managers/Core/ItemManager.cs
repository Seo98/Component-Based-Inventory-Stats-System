using UnityEngine;

public class ItemManager
{
    // Managers.Awake()에서 호출됨
    public void Init()
    {
        LoadDatabase();
    }

    private void LoadDatabase()
    {

        var db = Managers.Resource.Load<ItemDatabase>("ItemDatabase");

        if (db != null)
        {
            ItemDatabase.Initialize(db);
            Debug.Log("[ItemManager] DB 로드 성공");
        }
        else
        {
            Debug.LogError("[ItemManager] ItemDatabase 로드 실패! Addressable Name을 확인하세요.");
        }
    }
}