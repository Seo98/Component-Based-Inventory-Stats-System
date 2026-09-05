using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 기본값과 출처별 변경값을 조합해 최종 스탯 값을 계산합니다.
/// </summary>
[Serializable]
public sealed class CharacterStat
{
    private readonly List<StatModifier> _modifiers = new List<StatModifier>();
    private readonly ReadOnlyCollection<StatModifier> _readOnlyModifiers;

    private float _baseValue;
    private float _value;

    /// <summary>
    /// 최종값이 변경됐을 때 이전 값과 현재 값을 전달합니다.
    /// </summary>
    public event Action<float, float> ValueChanged;

    /// <summary>
    /// 장비, 버프 등의 변경값이 적용되기 전의 기본값입니다.
    /// </summary>
    public float BaseValue => _baseValue;

    /// <summary>
    /// 모든 변경값이 적용된 최종값입니다.
    /// </summary>
    public float Value => _value;

    /// <summary>
    /// 현재 적용 중인 변경값의 읽기 전용 목록입니다.
    /// </summary>
    public IReadOnlyList<StatModifier> Modifiers => _readOnlyModifiers;

    public CharacterStat(float baseValue = 0f)
    {
        _readOnlyModifiers = _modifiers.AsReadOnly();
        _baseValue = baseValue;
        _value = CalculateFinalValue();
    }

    /// <summary>
    /// 레벨업이나 저장 데이터 복구처럼 영구적인 기본값 변경에 사용합니다.
    /// </summary>
    public void SetBaseValue(float value)
    {
        if (_baseValue.Equals(value))
            return;

        _baseValue = value;
        RefreshValue();
    }

    /// <summary>
    /// 기본값에 지정한 값을 더합니다.
    /// </summary>
    public void AddBaseValue(float amount)
    {
        if (amount.Equals(0f))
            return;

        _baseValue += amount;
        RefreshValue();
    }

    /// <summary>
    /// 변경값 하나를 추가합니다.
    /// </summary>
    public void AddModifier(StatModifier modifier)
    {
        if (!modifier.Source.IsValid)
            throw new ArgumentException("Modifier source must be valid.", nameof(modifier));

        _modifiers.Add(modifier);
        RefreshValue();
    }

    /// <summary>
    /// 동일한 변경값 하나를 제거합니다.
    /// </summary>
    public bool RemoveModifier(StatModifier modifier)
    {
        if (!_modifiers.Remove(modifier))
            return false;

        RefreshValue();
        return true;
    }

    /// <summary>
    /// 지정한 출처에서 적용된 모든 변경값을 제거합니다.
    /// </summary>
    public bool RemoveModifiersFromSource(StatModifierSource source)
    {
        if (!source.IsValid)
            return false;

        int removedCount = 0;

        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            if (_modifiers[i].Source != source)
                continue;

            _modifiers.RemoveAt(i);
            removedCount++;
        }

        if (removedCount == 0)
            return false;

        RefreshValue();
        return true;
    }

    /// <summary>
    /// 적용 중인 모든 변경값을 제거합니다.
    /// </summary>
    public void ClearModifiers()
    {
        if (_modifiers.Count == 0)
            return;

        _modifiers.Clear();
        RefreshValue();
    }

    private void RefreshValue()
    {
        float previousValue = _value;
        float nextValue = CalculateFinalValue();

        if (previousValue.Equals(nextValue))
            return;

        _value = nextValue;
        ValueChanged?.Invoke(previousValue, nextValue);
    }

    private float CalculateFinalValue()
    {
        float flatTotal = 0f;
        float percentAddTotal = 0f;
        float finalValue = _baseValue;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier modifier = _modifiers[i];

            switch (modifier.Type)
            {
                case StatModifierType.Flat:
                    flatTotal += modifier.Value;
                    break;

                case StatModifierType.PercentAdd:
                    percentAddTotal += modifier.Value;
                    break;
            }
        }

        finalValue += flatTotal;
        finalValue *= 1f + percentAddTotal;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier modifier = _modifiers[i];

            if (modifier.Type == StatModifierType.PercentMultiply)
                finalValue *= 1f + modifier.Value;
        }

        return (float)Math.Round(finalValue, 4);
    }
}
