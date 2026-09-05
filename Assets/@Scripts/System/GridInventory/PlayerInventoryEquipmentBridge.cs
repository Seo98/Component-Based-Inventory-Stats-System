using UnityEngine;

/// <summary>
/// 로컬 인벤토리의 장비 변경 이벤트를 캐릭터의 서버 권한 장비 상태로 전달합니다.
/// 인벤토리와 스탯/외형/전투 사이에서 유일한 연결 지점으로 동작합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputRouter))]
[RequireComponent(typeof(NetworkCharacterEquipment))]
public sealed class PlayerInventoryEquipmentBridge : MonoBehaviour
{
    [SerializeField] private PlayerInventoryController _inventoryController;

    private PlayerInputRouter _inputRouter;
    private NetworkCharacterEquipment _equipmentState;
    private bool _isBound;

    private void Awake()
    {
        _inputRouter = GetComponent<PlayerInputRouter>();
        _equipmentState = GetComponent<NetworkCharacterEquipment>();
    }

    private void OnEnable()
    {
        PlayerInputRouter.LocalRouterChanged += HandleLocalRouterChanged;
        ItemDatabase.Initialized += HandleItemDatabaseInitialized;
        RefreshBinding(PlayerInputRouter.Local);
    }

    private void Start()
    {
        RefreshBinding(PlayerInputRouter.Local);
    }

    private void OnDisable()
    {
        PlayerInputRouter.LocalRouterChanged -= HandleLocalRouterChanged;
        ItemDatabase.Initialized -= HandleItemDatabaseInitialized;
        UnbindInventory();
    }

    private void HandleLocalRouterChanged(PlayerInputRouter localRouter)
    {
        RefreshBinding(localRouter);
    }

    private void HandleItemDatabaseInitialized()
    {
        if (_isBound)
            _inventoryController.PublishEquipmentState();
    }

    private void RefreshBinding(PlayerInputRouter localRouter)
    {
        if (localRouter != _inputRouter)
        {
            UnbindInventory();
            return;
        }

        BindInventory();
    }

    private void BindInventory()
    {
        if (_isBound)
            return;

        if (_inventoryController == null)
            _inventoryController = FindFirstObjectByType<PlayerInventoryController>();

        if (_inventoryController == null || _equipmentState == null)
            return;

        _inventoryController.EquipmentSlotChanged += HandleEquipmentSlotChanged;
        _inventoryController.OnHandItemChanged += HandleHandItemChanged;
        _isBound = true;
        _inventoryController.PublishEquipmentState();
    }

    private void UnbindInventory()
    {
        if (_inventoryController != null && _isBound)
        {
            _inventoryController.EquipmentSlotChanged -= HandleEquipmentSlotChanged;
            _inventoryController.OnHandItemChanged -= HandleHandItemChanged;
        }

        _isBound = false;
    }

    private void HandleEquipmentSlotChanged(
        EquipmentLoadoutSlot slot,
        ItemData item)
    {
        int itemId = item != null
            ? item.ItemID
            : NetworkCharacterEquipment.EmptyItemId;

        _equipmentState.RequestEquip(slot, itemId);
    }

    private void HandleHandItemChanged(ItemData item)
    {
        int itemId = item != null
            ? item.ItemID
            : NetworkCharacterEquipment.EmptyItemId;

        _equipmentState.RequestHeldItem(itemId);
    }
}
