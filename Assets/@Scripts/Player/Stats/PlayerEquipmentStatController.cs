using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 서버에서 동기화된 캐릭터 장비 상태를 런타임 스탯에 적용합니다.
/// 인벤토리 UI를 직접 참조하지 않으므로 서버와 모든 클라이언트가 같은 결과를 계산합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttributes))]
[RequireComponent(typeof(NetworkCharacterEquipment))]
public sealed class PlayerEquipmentStatController : MonoBehaviour
{
    private PlayerAttributes _attributes;
    private NetworkCharacterEquipment _equipmentState;
    private bool _isBound;

    private void Awake()
    {
        _attributes = GetComponent<PlayerAttributes>();
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
        _isBound = true;
        RebuildAllModifiers();
    }

    private void UnbindEquipmentState()
    {
        if (_equipmentState != null && _isBound)
        {
            _equipmentState.EquippedItemChanged -= HandleEquippedItemChanged;
        }

        _isBound = false;
        ClearAllModifiers();
    }

    private void HandleItemDatabaseInitialized()
    {
        RebuildAllModifiers();
    }

    private void HandleEquippedItemChanged(
        EquipmentLoadoutSlot slot,
        int previousItemId,
        int currentItemId)
    {
        ApplySlot(slot, currentItemId);
    }

    private void RebuildAllModifiers()
    {
        if (_equipmentState == null || _attributes == null)
            return;

        ClearAllModifiers();

        for (int i = 0; i < EquipmentLoadoutSlotUtility.SlotCount; i++)
        {
            EquipmentLoadoutSlot slot = (EquipmentLoadoutSlot)(i + 1);
            ApplySlot(slot, _equipmentState.GetEquippedItemId(slot));
        }
    }

    private void ApplySlot(EquipmentLoadoutSlot slot, int itemId)
    {
        RemoveSlotModifiers(slot);

        if (itemId == NetworkCharacterEquipment.EmptyItemId)
            return;

        if (!ItemDatabase.TryGetItem(itemId, out ItemData item))
        {
            Debug.LogWarning(
                $"[EquipmentStats] ItemData를 찾을 수 없습니다. ItemId={itemId}",
                this);
            return;
        }

        StatModifierSource source = CreateModifierSource(slot);
        IReadOnlyList<ItemStatModifierData> definitions = item.StatModifiers;

        for (int i = 0; i < definitions.Count; i++)
        {
            ItemStatModifierData definition = definitions[i];

            if (!_attributes.TryGetStat(definition.StatType, out CharacterStat stat))
            {
                Debug.LogWarning(
                    $"[EquipmentStats] 지원하지 않는 스탯입니다: {definition.StatType}",
                    this);
                continue;
            }

            stat.AddModifier(definition.CreateModifier(source));
        }

    }

    private void RemoveSlotModifiers(EquipmentLoadoutSlot slot)
    {
        StatModifierSource source = CreateModifierSource(slot);

        RemoveSource(_attributes.MaxStamina, source);
        RemoveSource(_attributes.Strength, source);
        RemoveSource(_attributes.Agility, source);
        RemoveSource(_attributes.Intelligence, source);
        RemoveSource(_attributes.Aggro, source);
        RemoveSource(_attributes.Stealth, source);
        RemoveSource(_attributes.InteractionSpeed, source);

        CharacterAttributes commonAttributes = _attributes.CommonAttributes;
        if (commonAttributes != null)
        {
            RemoveSource(commonAttributes.MaxHealth, source);
            RemoveSource(commonAttributes.AttackPower, source);
            RemoveSource(commonAttributes.Defense, source);
            RemoveSource(commonAttributes.MoveSpeed, source);
        }

    }

    private void ClearAllModifiers()
    {
        if (_attributes == null)
            return;

        for (int i = 0; i < EquipmentLoadoutSlotUtility.SlotCount; i++)
        {
            EquipmentLoadoutSlot slot = (EquipmentLoadoutSlot)(i + 1);
            RemoveSlotModifiers(slot);
        }
    }

    private static StatModifierSource CreateModifierSource(
        EquipmentLoadoutSlot slot)
    {
        return new StatModifierSource(
            StatModifierSourceType.Equipment,
            (long)slot);
    }

    private static void RemoveSource(
        CharacterStat stat,
        StatModifierSource source)
    {
        if (stat != null)
            stat.RemoveModifiersFromSource(source);
    }
}
