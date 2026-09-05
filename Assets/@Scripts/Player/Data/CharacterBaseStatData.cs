using UnityEngine;

/// <summary>
/// 플레이어와 몬스터가 공통으로 사용하는 기본 전투 및 이동 스탯 데이터입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "CharacterBaseStatData",
    menuName = "Project G/Character/Character Base Stat Data")]
public class CharacterBaseStatData : ScriptableObject
{
    [Header("Resources")]
    [SerializeField, Min(0f)] private float _maxHealth = 20f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float _attackPower;
    [SerializeField, Min(0f)] private float _defense;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed;

    /// <summary>기본 최대 체력입니다.</summary>
    public float MaxHealth
    {
        get
        {
            return _maxHealth;
        }
    }

    /// <summary>기본 공격력입니다.</summary>
    public float AttackPower
    {
        get
        {
            return _attackPower;
        }
    }

    /// <summary>기본 방어력입니다.</summary>
    public float Defense
    {
        get
        {
            return _defense;
        }
    }

    /// <summary>기본 이동 속도입니다.</summary>
    public float MoveSpeed
    {
        get
        {
            return _moveSpeed;
        }
    }
}
