using System;
using UnityEngine;

/// <summary>
/// 아이템 데이터에 저장되는 스탯 변경값 정의입니다.
/// 런타임 출처는 장착된 ItemInstance ID로 생성됩니다.
/// </summary>
[Serializable]
public struct ItemStatModifierData
{
    [SerializeField] private CharacterStatType _statType;
    [SerializeField] private StatModifierType _modifierType;

    [Tooltip("퍼센트 변경값은 20이 아니라 0.2처럼 입력합니다.")]
    [SerializeField] private float _value;

    public CharacterStatType StatType => _statType;
    public StatModifierType ModifierType => _modifierType;
    public float Value => _value;

    /// <summary>
    /// 지정한 장비 출처를 가진 런타임 Modifier를 생성합니다.
    /// </summary>
    public StatModifier CreateModifier(StatModifierSource source)
    {
        return new StatModifier(_value, _modifierType, source);
    }
}
