using System;

/// <summary>
/// 스탯 변경값의 계산 방식을 정의합니다.
/// 열거형 값은 계산 적용 순서로도 사용됩니다.
/// </summary>
public enum StatModifierType
{
    Flat = 100,
    PercentAdd = 200,
    PercentMultiply = 300
}

/// <summary>
/// 스탯 변경값을 발생시킨 시스템의 종류입니다.
/// </summary>
public enum StatModifierSourceType
{
    None = 0,
    Equipment = 1,
    Buff = 2,
    Passive = 3,
    System = 4
}

/// <summary>
/// 스탯 변경값을 발생시킨 런타임 출처를 식별합니다.
/// </summary>
public readonly struct StatModifierSource : IEquatable<StatModifierSource>
{
    public StatModifierSourceType Type { get; }
    public long Id { get; }

    /// <summary>
    /// 출처 제거에 사용할 수 있는 유효한 식별자인지 나타냅니다.
    /// </summary>
    public bool IsValid => Type != StatModifierSourceType.None && Id > 0;

    public StatModifierSource(StatModifierSourceType type, long id)
    {
        Type = type;
        Id = id;
    }

    public bool Equals(StatModifierSource other)
    {
        return Type == other.Type && Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is StatModifierSource other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Id);
    }

    public static bool operator ==(StatModifierSource left, StatModifierSource right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StatModifierSource left, StatModifierSource right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// 스탯 하나에 적용되는 불변 변경값입니다.
/// </summary>
public readonly struct StatModifier : IEquatable<StatModifier>
{
    public float Value { get; }
    public StatModifierType Type { get; }
    public StatModifierSource Source { get; }

    /// <summary>
    /// 변경값 종류에 따라 고정된 계산 순서입니다.
    /// </summary>
    public int Order => (int)Type;

    public StatModifier(
        float value,
        StatModifierType type,
        StatModifierSource source)
    {
        Value = value;
        Type = type;
        Source = source;
    }

    public bool Equals(StatModifier other)
    {
        return Value.Equals(other.Value) &&
               Type == other.Type &&
               Source.Equals(other.Source);
    }

    public override bool Equals(object obj)
    {
        return obj is StatModifier other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Type, Source);
    }

    public static bool operator ==(StatModifier left, StatModifier right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(StatModifier left, StatModifier right)
    {
        return !left.Equals(right);
    }
}
