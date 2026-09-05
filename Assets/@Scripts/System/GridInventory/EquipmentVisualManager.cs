using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서버에서 동기화된 캐릭터 장비 상태를 메시와 머티리얼로 표현합니다.
/// 인벤토리 UI나 RPC를 직접 다루지 않는 순수 외형 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkCharacterEquipment))]
public sealed class EquipmentVisualManager : MonoBehaviour
{
    [Serializable]
    public sealed class EquipmentBaseMapping
    {
        [Tooltip("이 외형이 담당하는 장비 종류입니다.")]
        public EquipmentSlot Slot;

        [Tooltip("손에 들었을 때 사용할 비주얼 또는 자세 타입입니다.")]
        public HandheldType HandheldType;

        [Header("Visual")]
        public GameObject BaseObject;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
    }

    [Header("Base Objects Setup")]
    [SerializeField] private List<EquipmentBaseMapping> _baseMappings =
        new List<EquipmentBaseMapping>();

    private NetworkCharacterEquipment _equipmentState;
    private bool _isBound;

    private void Awake()
    {
        _equipmentState = GetComponent<NetworkCharacterEquipment>();
    }

    private void OnEnable()
    {
        ItemDatabase.Initialized += HandleItemDatabaseInitialized;
        BindEquipmentState();
    }

    private void OnDisable()
    {
        ItemDatabase.Initialized -= HandleItemDatabaseInitialized;
        UnbindEquipmentState();
    }

    private void BindEquipmentState()
    {
        if (_isBound || _equipmentState == null)
            return;

        _equipmentState.EquippedItemChanged += HandleEquippedItemChanged;
        _equipmentState.HeldItemChanged += HandleHeldItemChanged;
        _isBound = true;
        RefreshAllVisuals();
    }

    private void UnbindEquipmentState()
    {
        if (_equipmentState != null && _isBound)
        {
            _equipmentState.EquippedItemChanged -= HandleEquippedItemChanged;
            _equipmentState.HeldItemChanged -= HandleHeldItemChanged;
        }

        _isBound = false;
    }

    private void HandleItemDatabaseInitialized()
    {
        RefreshAllVisuals();
    }

    private void HandleEquippedItemChanged(
        EquipmentLoadoutSlot slot,
        int previousItemId,
        int currentItemId)
    {
        switch (slot)
        {
            case EquipmentLoadoutSlot.Head:
                UpdateVisual(EquipmentSlot.Head, currentItemId);
                break;

            case EquipmentLoadoutSlot.Shoes:
                UpdateVisual(EquipmentSlot.Shoes, currentItemId);
                break;
        }
    }

    private void HandleHeldItemChanged(int previousItemId, int currentItemId)
    {
        UpdateVisual(EquipmentSlot.Weapon, currentItemId);
    }

    private void RefreshAllVisuals()
    {
        if (_equipmentState == null)
            return;

        UpdateVisual(EquipmentSlot.Weapon, _equipmentState.HeldItemId);
        UpdateVisual(
            EquipmentSlot.Head,
            _equipmentState.GetEquippedItemId(EquipmentLoadoutSlot.Head));
        UpdateVisual(
            EquipmentSlot.Shoes,
            _equipmentState.GetEquippedItemId(EquipmentLoadoutSlot.Shoes));
    }

    private void UpdateVisual(EquipmentSlot slot, int itemId)
    {
        ClearSlotVisual(slot);

        if (itemId == NetworkCharacterEquipment.EmptyItemId)
            return;

        if (!ItemDatabase.TryGetItem(itemId, out ItemData data))
            return;

        if (slot == EquipmentSlot.Weapon && !data.CanHoldInHand)
            return;

        for (int i = 0; i < _baseMappings.Count; i++)
        {
            EquipmentBaseMapping mapping = _baseMappings[i];
            if (mapping == null ||
                mapping.Slot != slot ||
                mapping.HandheldType != data.HandheldType)
            {
                continue;
            }

            ApplyVisual(mapping, data);
            break;
        }
    }

    private void ClearSlotVisual(EquipmentSlot slot)
    {
        for (int i = 0; i < _baseMappings.Count; i++)
        {
            EquipmentBaseMapping mapping = _baseMappings[i];
            if (mapping == null || mapping.Slot != slot)
                continue;

            if (mapping.BaseObject != null)
                mapping.BaseObject.SetActive(false);

            if (mapping.MeshFilter != null)
                mapping.MeshFilter.sharedMesh = null;
        }
    }

    private static void ApplyVisual(
        EquipmentBaseMapping mapping,
        ItemData data)
    {
        if (mapping.BaseObject != null)
            mapping.BaseObject.SetActive(true);

        if (mapping.MeshFilter != null && data.Mesh != null)
            mapping.MeshFilter.sharedMesh = data.Mesh;

        if (mapping.Renderer != null &&
            data.Materials != null &&
            data.Materials.Length > 0)
        {
            mapping.Renderer.sharedMaterials = data.Materials;
        }
    }
}
