using UnityEngine;

/// <summary>
/// 플레이어에게만 존재하는 기본 능력치 데이터입니다.
/// 실제 밸런스 값은 ScriptableObject 에셋에서 설정합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "PlayerBaseStatData",
    menuName = "Project G/Player/Player Base Stat Data")]
public sealed class PlayerBaseStatData : CharacterBaseStatData
{
    [Header("Resources")]
    [SerializeField, Min(0f)] private float _maxStamina = 20f;

    [Header("Primary Attributes")]
    [SerializeField, Min(0)] private int _strength;
    [SerializeField, Min(0)] private int _agility;
    [SerializeField, Min(0)] private int _intelligence;

    [Header("Utility Attributes")]
    [SerializeField, Min(0f)] private float _aggro;
    [SerializeField, Min(0f)] private float _stealth;
    [SerializeField, Min(0f)] private float _interactionSpeed;

    /// <summary>플레이어의 기본 최대 스태미나입니다.</summary>
    public float MaxStamina
    {
        get
        {
            return _maxStamina;
        }
    }

    /// <summary>플레이어의 기본 힘입니다.</summary>
    public float Strength
    {
        get
        {
            return _strength;
        }
    }

    /// <summary>플레이어의 기본 민첩입니다.</summary>
    public float Agility
    {
        get
        {
            return _agility;
        }
    }

    /// <summary>플레이어의 기본 지능입니다.</summary>
    public float Intelligence
    {
        get
        {
            return _intelligence;
        }
    }

    /// <summary>플레이어의 기본 어그로 수치입니다.</summary>
    public float Aggro
    {
        get
        {
            return _aggro;
        }
    }

    /// <summary>플레이어의 기본 은신 수치입니다.</summary>
    public float Stealth
    {
        get
        {
            return _stealth;
        }
    }

    /// <summary>플레이어의 기본 상호작용 속도입니다.</summary>
    public float InteractionSpeed
    {
        get
        {
            return _interactionSpeed;
        }
    }
}
