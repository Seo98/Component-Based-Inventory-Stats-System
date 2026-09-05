using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 시스템(데이터+로직)과 렌더러(UI)를 연결하고 관리하는 통합 클래스
/// </summary>
[RequireComponent(typeof(InventoryRenderer))]
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private InventoryRenderMode _renderMode = InventoryRenderMode.Grid;
    [SerializeField] private int _maximumAllowedItemCount = -1;
    [SerializeField] private ItemType _allowedItem = ItemType.Any;
    [SerializeField] private int _width = 8;
    [SerializeField] private int _height = 4;

    [Header("Initialization (Optional)")]
    [Tooltip("게임 시작 시 자동으로 인벤토리를 초기화할지 여부")]
    [SerializeField] private bool _initializeOnStart = true;
    [Tooltip("초기화 시 지급할 아이템 목록")]
    [SerializeField] private ItemData[] _initialItems = null;

    [Header("Runtime Debug Info (Read Only)")]
    // [수정] 호환성을 위해 타입을 인터페이스(IInventoryItem)나 상위 타입으로 관리하거나, 
    // 외부에서 주입될 때 ItemInstance로 캐스팅하여 담도록 합니다.
    [SerializeField] private List<ItemInstance> _currentInventoryItems = new List<ItemInstance>();

    // 실제 인벤토리 로직을 담당하는 객체
    public InventoryManager Inventory { get; private set; }
    private InventoryProvider _provider;
    private InventoryRenderer _renderer;
    private IInventoryManager _subscribedInventory;

    private void Awake()
    {
        _renderer = GetComponent<InventoryRenderer>();
    }

    private void Start()
    {
        if (_initializeOnStart)
        {
            Init();
        }
    }

    public void Init()
    {
        if (Inventory != null) return;

        _provider = new InventoryProvider(_renderMode, _maximumAllowedItemCount, _allowedItem);
        Inventory = new InventoryManager(_provider, _width, _height);

        _renderer.SetInventory(Inventory, _renderMode);

        SubscribeEvents();

        if (_initialItems != null && _initialItems.Length > 0)
        {
            InjectLoot(_initialItems);
        }
    }

    public void InitWithManager(InventoryManager externalManager)
    {
        if (externalManager == null)
        {
            Debug.LogError("[Inventory] 외부 InventoryManager가 null입니다.", this);
            return;
        }

        UnsubscribeEvents();
        Inventory = externalManager;
        _renderer.SetInventory(Inventory, _renderMode);

        RefreshDebugList();
        SubscribeEvents();
    }

    public void InjectLoot(ItemData[] lootItems)
    {
        if (Inventory == null) return;

        foreach (var data in lootItems)
        {
            if (data == null) continue;

            var newItem = data.CreateInstance();
            bool success = Inventory.TryAdd(newItem);

            if (!success)
            {
                Debug.LogWarning($"[Inventory] Full! Failed to add: {data.Name}");
            }
        }
    }

    private void SubscribeEvents()
    {
        UnsubscribeEvents();
        _subscribedInventory = Inventory;

        if (_subscribedInventory == null)
            return;

        _subscribedInventory.onItemAdded += OnItemAdded;
        _subscribedInventory.onItemRemoved += OnItemRemoved;
        _subscribedInventory.onItemDropped += OnItemDropped;
        _subscribedInventory.onItemDroppedFailed += OnItemDroppedFailed;
        _subscribedInventory.onItemAddedFailed += OnItemAddedFailed;
    }

    private void UnsubscribeEvents()
    {
        if (_subscribedInventory == null)
            return;

        _subscribedInventory.onItemAdded -= OnItemAdded;
        _subscribedInventory.onItemRemoved -= OnItemRemoved;
        _subscribedInventory.onItemDropped -= OnItemDropped;
        _subscribedInventory.onItemDroppedFailed -= OnItemDroppedFailed;
        _subscribedInventory.onItemAddedFailed -= OnItemAddedFailed;
        _subscribedInventory = null;
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void OnItemAdded(IInventoryItem item)
    {
        if (item is ItemInstance instance)
        {
            if (!_currentInventoryItems.Contains(instance))
                _currentInventoryItems.Add(instance);
        }
    }

    private void OnItemRemoved(IInventoryItem item)
    {
        if (item is ItemInstance instance)
        {
            if (_currentInventoryItems.Contains(instance))
            {
                _currentInventoryItems.Remove(instance);
            }
        }
    }

    private void LogItemAction(string action, IInventoryItem item)
    {
        string itemName = (item is ItemInstance inst) ? inst.Name : "Unknown Item";
        Debug.Log($"[Inven] {action}: {itemName}");
    }

    private void OnItemDropped(IInventoryItem item) => LogItemAction("Dropped", item);

    private void OnItemDroppedFailed(IInventoryItem item) => LogItemAction("Drop Failed", item);

    private void OnItemAddedFailed(IInventoryItem item) => LogItemAction("Add Failed", item);

    private void RefreshDebugList()
    {
        _currentInventoryItems.Clear();

        if (Inventory != null)
        {
            foreach (var item in Inventory.allItems)
            {
                if (item is ItemInstance instance)
                {
                    _currentInventoryItems.Add(instance);
                }
            }
        }
    }

    // ========================================================================
    // [수정됨] TransformInteract와의 호환성을 위한 메서드 추가 및 수정 구역
    // ========================================================================

    /// <summary>
    /// [수정됨] 매개변수 타입을 List<ItemInstance> -> List<IInventoryItem>으로 변경하여 
    /// 인터페이스 리스트를 반환하는 외부 시스템(Rootable 등)과 호환되게 함.
    /// </summary>
    public void AddExistingItems(List<IInventoryItem> existingItems)
    {
        if (Inventory == null) return;

        foreach (var item in existingItems)
        {
            if (item == null) continue;

            // 저장된 위치(position)에 아이템 배치 시도
            bool success = Inventory.TryAddAt(item, item.position);

            if (!success)
            {
                // 실패 시 위치 상관없이 빈 곳에 추가 시도 (옵션)
                // Inventory.TryAdd(item); 
                Debug.LogWarning($"[Inventory] Failed to restore item at {item.position}: {(item is ItemInstance inst ? inst.Name : "Unknown")}");
            }
        }
        // 이벤트에 의해 리스트가 갱신되지만, 확실하게 하기 위해 호출
        RefreshDebugList();
    }

    /// <summary>
    /// 외부 루팅 대상과의 표시 연결을 끊고 임시 인벤토리 데이터를 비웁니다.
    /// </summary>
    public void DisconnectInventory()
    {
        if (Inventory != null)
        {
            Inventory.Clear();
        }

        _currentInventoryItems.Clear();
    }

    /// <summary>
    /// [추가됨] 기존 SizeInventoryExample의 동작을 모방.
    /// 외부 리스트(connector)를 받아와 현재 디버그 리스트에 연결합니다.
    /// </summary>
    public void ConnectInventory(List<IInventoryItem> connector)
    {
        _currentInventoryItems.Clear();

        if (connector != null)
        {
            foreach (var item in connector)
            {
                if (item is ItemInstance instance)
                {
                    _currentInventoryItems.Add(instance);
                }
            }
        }
    }
}
