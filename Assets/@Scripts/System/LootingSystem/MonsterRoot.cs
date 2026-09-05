using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 임시 네트워크 루팅 대상입니다. 아이템 목록 변경은 서버만 확정하고 NetworkList로 모든 클라이언트에 전파합니다.
/// </summary>
public class MonsterRoot : NetworkBehaviour,IInteractable,IRootable
{
    public NetworkList<ItemDataPayload> itemDatas;
    public List<IInventoryItem> connector;
    public HashSet<ulong> connectedPlayers;
    [SerializeField] private List<ItemData> itemDataBase;

    private void Awake()
    {
        itemDatas = new NetworkList<ItemDataPayload>();
        connector = new List<IInventoryItem>();
    }
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (itemDataBase == null || itemDataBase.Count == 0)
        {
            Debug.LogWarning("MonsterRoot에 테스트용 Item Data를 하나 이상 지정해야 합니다.", this);
            return;
        }
        
        int randInt = Random.Range(0, itemDataBase.Count);
        
        ItemInstance item = itemDataBase[randInt].CreateInstance();
        ItemDataPayload payload = new ItemDataPayload();
        payload = LoadInstance(item);
        itemDatas.Add(payload);
    }

    public ItemDataPayload LoadInstance(IInventoryItem item)
    {
        ItemDataPayload payload = new ItemDataPayload();
        payload.InstanceID = item.InstanceID;
        payload.ItemID = item.Data.ItemID;
        payload.ItemTypeID = (int)item.Data.Type;
        payload.X = item.position.x;
        payload.Y = item.position.y;
        payload.width = item.width;
        payload.height = item.height;
        payload.rotation = item.Rotation;

        return payload;
    }

    public void TransformInteract(Transform Transform)
    {
        return;
    }

    public string GetInteractionText()
    {
        return "monster root";
    }

    public string GetTag()
    {
        return this.gameObject.tag;
    }

    public void AddItem(IInventoryItem item)
    {
        if (item == null) return;

        ItemDataPayload payload = LoadInstance(item);
        if (IsServer)
        {
            AddItemOnServer(payload);
            return;
        }

        AddItemServerRpc(payload);
    }

    public void RemoveItem(IInventoryItem item)
    {
        if (item == null) return;

        if (IsServer)
        {
            RemoveItemOnServer(item.InstanceID);
            return;
        }

        RemoveItemServerRpc(item.InstanceID);
    }

    public void DropItem(IInventoryItem item)
    {
        RemoveItem(item);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AddItemServerRpc(ItemDataPayload payload)
    {
        AddItemOnServer(payload);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RemoveItemServerRpc(int instanceId)
    {
        RemoveItemOnServer(instanceId);
    }

    private void AddItemOnServer(ItemDataPayload payload)
    {
        for (int i = 0; i < itemDatas.Count; i++)
        {
            if (itemDatas[i].InstanceID == payload.InstanceID)
                return;
        }

        itemDatas.Add(payload);
    }

    private void RemoveItemOnServer(int instanceId)
    {
        for (int i = 0; i < itemDatas.Count; i++)
        {
            if (itemDatas[i].InstanceID != instanceId)
                continue;

            itemDatas.RemoveAt(i);
            return;
        }
    }

    public List<IInventoryItem> GetConnector()
    {
        connector.Clear();
        return connector;
    }
    
    public List<IInventoryItem> GetItems()
    {
        List<IInventoryItem> items = new List<IInventoryItem>();
        foreach(var data in itemDatas)
        {
            ItemData itemData = itemDataBase.Find(candidate => candidate != null && candidate.ItemID == data.ItemID);
            if (itemData == null)
            {
                Debug.LogWarning($"MonsterRoot가 ItemID {data.ItemID}의 ItemData를 찾지 못했습니다.", this);
                continue;
            }

            ItemInstance item = new ItemInstance(itemData, data.InstanceID);
            item.position = new Vector2Int(data.X, data.Y);
            item.Rotation = data.rotation;
            items.Add(item);
        }
        return items;
    }
}

/// <summary>
/// 네트워크로 동기화할 인벤토리 아이템의 최소 상태입니다.
/// ScriptableObject 자체는 전송하지 않고, 양쪽 프로젝트에 공통으로 존재하는 ItemID만 전송합니다.
/// </summary>
public struct ItemDataPayload : INetworkSerializable, System.IEquatable<ItemDataPayload>
{
    public int InstanceID;
    public int ItemID;
    public int ItemTypeID;
    public int X;
    public int Y;
    public int width;
    public int height;
    public float rotation;

    /// <summary>아이템 상태를 Netcode 버퍼에 직렬화하거나 역직렬화합니다.</summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InstanceID);
        serializer.SerializeValue(ref ItemID);
        serializer.SerializeValue(ref ItemTypeID);
        serializer.SerializeValue(ref X);
        serializer.SerializeValue(ref Y);
        serializer.SerializeValue(ref width);
        serializer.SerializeValue(ref height);
        serializer.SerializeValue(ref rotation);
    }

    public bool Equals(ItemDataPayload other)
    {
        return InstanceID == other.InstanceID
               && ItemID == other.ItemID
               && ItemTypeID == other.ItemTypeID
               && X == other.X
               && Y == other.Y
               && width == other.width
               && height == other.height
               && rotation.Equals(other.rotation);
    }
}
