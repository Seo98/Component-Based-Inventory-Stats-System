using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터의 장착 아이템과 손에 든 아이템 ID를 서버 권한 상태로 보관합니다.
/// 인벤토리, 스탯, 외형 시스템은 이 상태를 사이에 두고 서로를 직접 참조하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkCharacterEquipment : NetworkBehaviour
{
    public const int EmptyItemId = -1;

    private readonly NetworkList<int> _equippedItemIds = new NetworkList<int>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _heldItemId = new NetworkVariable<int>(
        EmptyItemId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly int[] _offlineEquippedItemIds =
        new int[EquipmentLoadoutSlotUtility.SlotCount];

    private int _offlineHeldItemId = EmptyItemId;

    /// <summary>장비 슬롯의 아이템 ID가 변경됐을 때 발생합니다.</summary>
    public event Action<EquipmentLoadoutSlot, int, int> EquippedItemChanged;

    /// <summary>손에 든 아이템 ID가 변경됐을 때 발생합니다.</summary>
    public event Action<int, int> HeldItemChanged;

    /// <summary>현재 손에 든 아이템 ID입니다. 비어 있으면 -1입니다.</summary>
    public int HeldItemId => IsSpawned ? _heldItemId.Value : _offlineHeldItemId;

    private void Awake()
    {
        for (int i = 0; i < _offlineEquippedItemIds.Length; i++)
            _offlineEquippedItemIds[i] = EmptyItemId;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            InitializeServerSlots();

        _equippedItemIds.OnListChanged += HandleEquippedItemsChanged;
        _heldItemId.OnValueChanged += HandleHeldItemChanged;
        PublishCurrentState();
    }

    public override void OnNetworkDespawn()
    {
        CopyNetworkStateToOfflineState();
        _equippedItemIds.OnListChanged -= HandleEquippedItemsChanged;
        _heldItemId.OnValueChanged -= HandleHeldItemChanged;
    }

    /// <summary>
    /// 소유 클라이언트가 장비 변경을 서버에 요청합니다.
    /// 반환값은 요청 전달 여부이며, 최종 승인은 서버가 아이템과 슬롯을 검증한 뒤 결정합니다.
    /// </summary>
    public bool RequestEquip(EquipmentLoadoutSlot slot, int itemId)
    {
        if (!EquipmentLoadoutSlotUtility.IsValid(slot))
            return false;

        if (!IsSpawned)
            return TrySetOfflineEquipment(slot, itemId);

        if (!IsOwner && !IsServer)
            return false;

        if (IsServer)
            return TrySetServerEquipment(slot, itemId);

        RequestEquipRpc(slot, itemId);
        return true;
    }

    /// <summary>
    /// 소유 클라이언트가 손에 들 아이템 변경을 서버에 요청합니다.
    /// 손에 들 수 있도록 설정된 ItemData만 서버가 승인합니다.
    /// </summary>
    public bool RequestHeldItem(int itemId)
    {
        if (!IsSpawned)
            return TrySetOfflineHeldItem(itemId);

        if (!IsOwner && !IsServer)
            return false;

        if (IsServer)
            return TrySetServerHeldItem(itemId);

        RequestHeldItemRpc(itemId);
        return true;
    }

    /// <summary>현재 장비 슬롯에 저장된 ItemData ID를 반환합니다.</summary>
    public int GetEquippedItemId(EquipmentLoadoutSlot slot)
    {
        if (!EquipmentLoadoutSlotUtility.IsValid(slot))
            return EmptyItemId;

        int index = EquipmentLoadoutSlotUtility.ToIndex(slot);

        if (IsSpawned)
        {
            if (index >= 0 && index < _equippedItemIds.Count)
                return _equippedItemIds[index];

            return EmptyItemId;
        }

        return _offlineEquippedItemIds[index];
    }

    /// <summary>현재 장비와 손 아이템 상태를 구독자에게 다시 전달합니다.</summary>
    public void PublishCurrentState()
    {
        for (int i = 0; i < EquipmentLoadoutSlotUtility.SlotCount; i++)
        {
            EquipmentLoadoutSlot slot = (EquipmentLoadoutSlot)(i + 1);
            int itemId = GetEquippedItemId(slot);
            EquippedItemChanged?.Invoke(slot, EmptyItemId, itemId);
        }

        HeldItemChanged?.Invoke(EmptyItemId, HeldItemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestEquipRpc(EquipmentLoadoutSlot slot, int itemId)
    {
        TrySetServerEquipment(slot, itemId);
    }

    [Rpc(SendTo.Server)]
    private void RequestHeldItemRpc(int itemId)
    {
        TrySetServerHeldItem(itemId);
    }

    private bool TrySetServerEquipment(EquipmentLoadoutSlot slot, int itemId)
    {
        if (!IsServer || !ValidateEquipment(slot, itemId))
            return false;

        int index = EquipmentLoadoutSlotUtility.ToIndex(slot);
        if (index < 0 || index >= _equippedItemIds.Count)
            return false;

        if (_equippedItemIds[index] == itemId)
            return true;

        _equippedItemIds[index] = itemId;
        return true;
    }

    private bool TrySetServerHeldItem(int itemId)
    {
        if (!IsServer || !ValidateHeldItem(itemId))
            return false;

        if (_heldItemId.Value == itemId)
            return true;

        _heldItemId.Value = itemId;
        return true;
    }

    private bool TrySetOfflineEquipment(EquipmentLoadoutSlot slot, int itemId)
    {
        if (!ValidateEquipment(slot, itemId))
            return false;

        int index = EquipmentLoadoutSlotUtility.ToIndex(slot);
        int previousItemId = _offlineEquippedItemIds[index];

        if (previousItemId == itemId)
            return true;

        _offlineEquippedItemIds[index] = itemId;
        EquippedItemChanged?.Invoke(slot, previousItemId, itemId);
        return true;
    }

    private bool TrySetOfflineHeldItem(int itemId)
    {
        if (!ValidateHeldItem(itemId))
            return false;

        int previousItemId = _offlineHeldItemId;
        if (previousItemId == itemId)
            return true;

        _offlineHeldItemId = itemId;
        HeldItemChanged?.Invoke(previousItemId, itemId);
        return true;
    }

    private static bool ValidateEquipment(EquipmentLoadoutSlot slot, int itemId)
    {
        if (!EquipmentLoadoutSlotUtility.IsValid(slot))
            return false;

        if (itemId == EmptyItemId)
            return true;

        if (!ItemDatabase.TryGetItem(itemId, out ItemData item))
            return false;

        return item.EquipSlot == GetRequiredItemSlot(slot);
    }

    private static bool ValidateHeldItem(int itemId)
    {
        if (itemId == EmptyItemId)
            return true;

        return ItemDatabase.TryGetItem(itemId, out ItemData item) && item.CanHoldInHand;
    }

    private static EquipmentSlot GetRequiredItemSlot(EquipmentLoadoutSlot slot)
    {
        switch (slot)
        {
            case EquipmentLoadoutSlot.Weapon:
                return EquipmentSlot.Weapon;

            case EquipmentLoadoutSlot.Head:
                return EquipmentSlot.Head;

            case EquipmentLoadoutSlot.Shoes:
                return EquipmentSlot.Shoes;

            case EquipmentLoadoutSlot.Ring1:
            case EquipmentLoadoutSlot.Ring2:
                return EquipmentSlot.Ring;

            case EquipmentLoadoutSlot.Amulet:
                return EquipmentSlot.Amulet;

            default:
                return EquipmentSlot.None;
        }
    }

    private void InitializeServerSlots()
    {
        while (_equippedItemIds.Count < EquipmentLoadoutSlotUtility.SlotCount)
            _equippedItemIds.Add(EmptyItemId);

        while (_equippedItemIds.Count > EquipmentLoadoutSlotUtility.SlotCount)
            _equippedItemIds.RemoveAt(_equippedItemIds.Count - 1);
    }

    private void HandleEquippedItemsChanged(NetworkListEvent<int> changeEvent)
    {
        if (changeEvent.Index < 0 ||
            changeEvent.Index >= EquipmentLoadoutSlotUtility.SlotCount)
        {
            return;
        }

        EquipmentLoadoutSlot slot =
            (EquipmentLoadoutSlot)(changeEvent.Index + 1);

        EquippedItemChanged?.Invoke(
            slot,
            changeEvent.PreviousValue,
            changeEvent.Value);
    }

    private void HandleHeldItemChanged(int previousItemId, int currentItemId)
    {
        HeldItemChanged?.Invoke(previousItemId, currentItemId);
    }

    private void CopyNetworkStateToOfflineState()
    {
        for (int i = 0; i < _offlineEquippedItemIds.Length; i++)
        {
            _offlineEquippedItemIds[i] = i < _equippedItemIds.Count
                ? _equippedItemIds[i]
                : EmptyItemId;
        }

        _offlineHeldItemId = _heldItemId.Value;
    }
}
