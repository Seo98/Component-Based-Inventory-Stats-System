using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventoryController : MonoBehaviour
{
    [Header("System UI")]
    [SerializeField] private GameObject _inventoryCanvas;
    [SerializeField] private PlayerInventory _lootInventoryUI;
    [SerializeField] private PlayerInventory _mainInventoryUI;
    public PlayerInventory LootUI => _lootInventoryUI;

    [Header("Equipment Slots (UI)")]
    [SerializeField] private PlayerInventory _weaponSlotUI1;     // Key: 1
    [SerializeField] private PlayerInventory _headArmorSlotUI;
    [SerializeField] private PlayerInventory _footArmorSlotUI;
    [SerializeField] private PlayerInventory _ringSlotUI1;
    [SerializeField] private PlayerInventory _ringSlotUI2;
    [SerializeField] private PlayerInventory _amuletSlotUI;

    [Header("Quick Slots (UI)")]
    [SerializeField] private PlayerInventory _quickSlotUI1;      // Key: 3
    [SerializeField] private PlayerInventory _quickSlotUI2;      // Key: 4
    [SerializeField] private PlayerInventory _quickSlotUI3;      // Key: 5

    [Header("Runtime Data (Read Only)")]
    [SerializeField] private List<ItemData> _mainInventoryItems = new List<ItemData>();
    [SerializeField] private List<IInventoryItem> _currentLootItems;

    // 현재 슬롯 데이터 캐싱
    [SerializeField] private ItemInstance _equippedWeapon1;
    [SerializeField] private ItemInstance _equippedHead;
    [SerializeField] private ItemInstance _equippedFoot;
    [SerializeField] private ItemInstance _equippedRing1;
    [SerializeField] private ItemInstance _equippedRing2;
    [SerializeField] private ItemInstance _equippedAmulet;

    [SerializeField] private ItemInstance _quickItem1;
    [SerializeField] private ItemInstance _quickItem2;
    [SerializeField] private ItemInstance _quickItem3;

    // 현재 실제로 손에 들고 있는 아이템
    [SerializeField] private ItemInstance _currentHeldItem;
    private bool isInventoryOpen = false;
    private PlayerInputRouter _inputRouter;

    /// <summary>
    /// 외부 시스템에서 현재 손에 든 아이템을 읽을 수 있는 프로퍼티
    /// </summary>
    public ItemData CurrentHeldItem => _currentHeldItem != null ? _currentHeldItem.Data : null;

    /// <summary>
    /// 현재 손에 든 개별 아이템 인스턴스입니다.
    /// </summary>
    public ItemInstance CurrentHeldItemInstance => _currentHeldItem;

    /// <summary>
    /// 외부 시스템에서 인벤토리가 열려있는지 확인할 수 있는 프로퍼티 (공격/이동 제한용)
    /// </summary>
    public bool IsInventoryOpen => isInventoryOpen;

    /// <summary>
    /// 손에 든 아이템이 변경되었을 때 외부 연결 컴포넌트에 알리는 이벤트입니다.
    /// </summary>
    public event Action<ItemData> OnHandItemChanged;

    /// <summary>
    /// 실제 장비 위치의 ItemData가 변경됐을 때 발생합니다.
    /// 아이템이 제거된 경우 ItemData는 null입니다.
    /// </summary>
    public event Action<EquipmentLoadoutSlot, ItemData> EquipmentSlotChanged;

    /// <summary>
    /// 아이템 인스턴스가 장비 슬롯에 들어갔을 때 발생합니다.
    /// </summary>
    public event Action<ItemInstance> ItemEquipped;

    /// <summary>
    /// 아이템 인스턴스가 장비 슬롯에서 제거됐을 때 발생합니다.
    /// </summary>
    public event Action<ItemInstance> ItemUnequipped;

    private readonly List<IDisposable> _inventoryEventBindings = new List<IDisposable>();

    private void OnEnable()
    {
        PlayerInputRouter.LocalRouterChanged += HandleLocalInputRouterChanged;
        BindInputRouter(PlayerInputRouter.Local);
    }

    private void OnDisable()
    {
        PlayerInputRouter.LocalRouterChanged -= HandleLocalInputRouterChanged;
        BindInputRouter(null);
    }

    private void Start()
    {
        // 인벤토리 시스템 초기화
        SetupInventories(
            _mainInventoryUI, _lootInventoryUI,
            _weaponSlotUI1,
            _headArmorSlotUI, _footArmorSlotUI,
            _ringSlotUI1, _ringSlotUI2, _amuletSlotUI,
            _quickSlotUI1, _quickSlotUI2, _quickSlotUI3
        );

        // 이벤트 구독
        SubscribeToEvents();

        // UI 초기 상태 설정
        if (_inventoryCanvas != null) _inventoryCanvas.SetActive(false);
        if (_lootInventoryUI != null) _lootInventoryUI.gameObject.SetActive(false);
    }

    private void HandleInventoryToggleRequested()
    {
        if (!isInventoryOpen &&
            _inputRouter != null &&
            _inputRouter.IsGameplayBlockedByOtherThan(this))
        {
            return;
        }

        ToggleInventory();
    }

    private void HandleCancelRequested()
    {
        if (isInventoryOpen)
            ToggleInventory();
    }

    private void HandleHotbarRequested(int slot)
    {
        if (isInventoryOpen)
            return;

        switch (slot)
        {
            case 1:
                EquipHandItem(_equippedWeapon1);
                break;
            case 3:
                EquipHandItem(_quickItem1);
                break;
            case 4:
                EquipHandItem(_quickItem2);
                break;
            case 5:
                EquipHandItem(_quickItem3);
                break;
        }
    }

    private void HandleUnequipRequested()
    {
        if (!isInventoryOpen)
            EquipHandItem(null);
    }

    private void HandleLocalInputRouterChanged(PlayerInputRouter router)
    {
        BindInputRouter(router);
    }

    private void BindInputRouter(PlayerInputRouter router)
    {
        if (_inputRouter == router)
            return;

        if (_inputRouter != null)
        {
            _inputRouter.InventoryToggleRequested -= HandleInventoryToggleRequested;
            _inputRouter.CancelRequested -= HandleCancelRequested;
            _inputRouter.HotbarRequested -= HandleHotbarRequested;
            _inputRouter.UnequipRequested -= HandleUnequipRequested;
            _inputRouter.SetGameplayBlocked(this, false);
        }

        _inputRouter = router;

        if (_inputRouter == null)
            return;

        _inputRouter.InventoryToggleRequested += HandleInventoryToggleRequested;
        _inputRouter.CancelRequested += HandleCancelRequested;
        _inputRouter.HotbarRequested += HandleHotbarRequested;
        _inputRouter.UnequipRequested += HandleUnequipRequested;
        _inputRouter.SetGameplayBlocked(this, isInventoryOpen);
    }

    /// <summary>
    /// 손에 든 아이템 상태를 변경하고 외부 연결 컴포넌트에 알립니다.
    /// </summary>
    private void EquipHandItem(ItemInstance itemToEquip)
    {
        ItemData itemData = itemToEquip != null ? itemToEquip.Data : null;

        if (itemData != null && !itemData.CanHoldInHand)
        {
            Debug.LogWarning($"[Inventory] 손에 들 수 없는 아이템입니다: {itemData.Name}");
            return;
        }

        _currentHeldItem = itemToEquip;
        OnHandItemChanged?.Invoke(itemData);
    }

    // --- 이벤트 구독 및 데이터 동기화 ---
    private void SubscribeToEvents()
    {
        ClearInventoryEventBindings();

        if (_mainInventoryUI != null && _mainInventoryUI.Inventory != null)
        {
            _inventoryEventBindings.Add(new InventorySlotEventBinding(
                _mainInventoryUI.Inventory,
                SyncMainInventoryData,
                SyncMainInventoryData,
                SyncMainInventoryData));
            SyncMainInventoryData(null);
        }

        // 무기: 슬롯에 넣는다고 바로 들지 않음 (숫자 키 입력 시 손에 장착)
        BindSlotEvent(_weaponSlotUI1,
            (item) => {
                _equippedWeapon1 = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Weapon,
                    _equippedWeapon1);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedWeapon1;
                if (_currentHeldItem == removed) EquipHandItem(null);
                _equippedWeapon1 = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Weapon, removed);
            });

        // 머리 방어구
        BindSlotEvent(_headArmorSlotUI,
            (item) => {
                _equippedHead = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Head,
                    _equippedHead);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedHead;
                _equippedHead = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Head, removed);
            });

        // 신발 방어구
        BindSlotEvent(_footArmorSlotUI,
            (item) => {
                _equippedFoot = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Shoes,
                    _equippedFoot);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedFoot;
                _equippedFoot = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Shoes, removed);
            });

        // 장신구류 (일반적으로 외형 변화 없음)
        BindSlotEvent(_ringSlotUI1,
            (item) => {
                _equippedRing1 = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Ring1,
                    _equippedRing1);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedRing1;
                _equippedRing1 = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Ring1, removed);
            });

        BindSlotEvent(_ringSlotUI2,
            (item) => {
                _equippedRing2 = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Ring2,
                    _equippedRing2);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedRing2;
                _equippedRing2 = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Ring2, removed);
            });

        BindSlotEvent(_amuletSlotUI,
            (item) => {
                _equippedAmulet = GetItemInstance(item);
                NotifyItemEquipped(
                    EquipmentLoadoutSlot.Amulet,
                    _equippedAmulet);
            },
            (item) => {
                ItemInstance removed = GetItemInstance(item) ?? _equippedAmulet;
                _equippedAmulet = null;
                NotifyItemUnequipped(EquipmentLoadoutSlot.Amulet, removed);
            });

        BindSlotEvent(_quickSlotUI1,
            (item) => _quickItem1 = GetItemInstance(item),
            (item) => { if (_currentHeldItem == _quickItem1) EquipHandItem(null); _quickItem1 = null; });

        BindSlotEvent(_quickSlotUI2,
            (item) => _quickItem2 = GetItemInstance(item),
            (item) => { if (_currentHeldItem == _quickItem2) EquipHandItem(null); _quickItem2 = null; });

        BindSlotEvent(_quickSlotUI3,
            (item) => _quickItem3 = GetItemInstance(item),
            (item) => { if (_currentHeldItem == _quickItem3) EquipHandItem(null); _quickItem3 = null; });
    }

    private void BindSlotEvent(PlayerInventory ui, Action<IInventoryItem> onAdd, Action<IInventoryItem> onRemove)
    {
        if (ui == null || ui.Inventory == null) return;
        _inventoryEventBindings.Add(new InventorySlotEventBinding(ui.Inventory, onAdd, onRemove));

        IInventoryItem initialItem = GetFirstItem(ui.Inventory);
        if (initialItem != null)
            onAdd?.Invoke(initialItem);
    }

    private void ClearInventoryEventBindings()
    {
        foreach (IDisposable binding in _inventoryEventBindings)
        {
            binding.Dispose();
        }

        _inventoryEventBindings.Clear();
    }

    private void OnDestroy()
    {
        BindInputRouter(null);
        ClearInventoryEventBindings();
    }

    private void SyncMainInventoryData(IInventoryItem item)
    {
        _mainInventoryItems.Clear();
        if (_mainInventoryUI == null || _mainInventoryUI.Inventory == null) return;
        foreach (var invItem in _mainInventoryUI.Inventory.allItems)
        {
            var data = GetItemData(invItem);
            if (data != null) _mainInventoryItems.Add(data);
        }
    }

    private ItemData GetItemData(IInventoryItem item)
    {
        if (item == null) return null;
        if (item is ItemInstance instance) return instance.Data;
        return null;
    }

    private ItemInstance GetItemInstance(IInventoryItem item)
    {
        return item as ItemInstance;
    }

    private IInventoryItem GetFirstItem(InventoryManager inventory)
    {
        if (inventory == null || inventory.allItems == null)
            return null;

        for (int i = 0; i < inventory.allItems.Length; i++)
        {
            if (inventory.allItems[i] != null)
                return inventory.allItems[i];
        }

        return null;
    }

    private void NotifyItemEquipped(
        EquipmentLoadoutSlot slot,
        ItemInstance item)
    {
        if (item != null)
            ItemEquipped?.Invoke(item);

        ItemData itemData = item != null ? item.Data : null;
        EquipmentSlotChanged?.Invoke(slot, itemData);
    }

    private void NotifyItemUnequipped(
        EquipmentLoadoutSlot slot,
        ItemInstance item)
    {
        if (item != null)
            ItemUnequipped?.Invoke(item);

        EquipmentSlotChanged?.Invoke(slot, null);
    }

    /// <summary>
    /// 현재 장비 슬롯 상태를 구독자에게 다시 전달합니다.
    /// </summary>
    public void PublishEquippedItems()
    {
        NotifyItemEquipped(EquipmentLoadoutSlot.Weapon, _equippedWeapon1);
        NotifyItemEquipped(EquipmentLoadoutSlot.Head, _equippedHead);
        NotifyItemEquipped(EquipmentLoadoutSlot.Shoes, _equippedFoot);
        NotifyItemEquipped(EquipmentLoadoutSlot.Ring1, _equippedRing1);
        NotifyItemEquipped(EquipmentLoadoutSlot.Ring2, _equippedRing2);
        NotifyItemEquipped(EquipmentLoadoutSlot.Amulet, _equippedAmulet);
    }

    /// <summary>
    /// 현재 장비 슬롯과 손 아이템 상태를 외부 연결 컴포넌트에 다시 전달합니다.
    /// </summary>
    public void PublishEquipmentState()
    {
        PublishEquippedItems();
        OnHandItemChanged?.Invoke(CurrentHeldItem);
    }

    private void SetupInventories(params PlayerInventory[] inventories)
    {
        foreach (var inventory in inventories)
        {
            if (inventory != null)
            {
                inventory.Init();
            }
        }
    }

    public void ToggleInventory()
    {
        bool shouldOpen = !isInventoryOpen;
        if (!shouldOpen)
        {
            InventoryController.CancelActiveDrag();
        }

        isInventoryOpen = shouldOpen;
        if (_inputRouter != null)
            _inputRouter.SetGameplayBlocked(this, isInventoryOpen);

        if (_inventoryCanvas != null) _inventoryCanvas.SetActive(isInventoryOpen);

        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CloseLootContainer();
        }
    }

    public void OpenLootContainer(InventoryManager chestManager)
    {
        if (_lootInventoryUI == null) return;
        if (!isInventoryOpen) ToggleInventory();
        _lootInventoryUI.gameObject.SetActive(true);

        if (chestManager != null) _lootInventoryUI.InitWithManager(chestManager);
        else _lootInventoryUI.Init();
    }

    public void CloseLootContainer()
    {
        if (_lootInventoryUI != null) _lootInventoryUI.gameObject.SetActive(false);
    }

}
