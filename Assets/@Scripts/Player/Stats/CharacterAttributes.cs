using System;
using UnityEngine;

/// <summary>
/// 플레이어와 몬스터가 공통으로 사용하는 런타임 스탯을 소유합니다.
/// </summary>
public sealed class CharacterAttributes : MonoBehaviour
{
    [SerializeField] private CharacterBaseStatData _baseStatData;

    /// <summary>런타임 스탯을 생성할 때 사용하는 원본 데이터입니다.</summary>
    public CharacterBaseStatData BaseStatData
    {
        get
        {
            return _baseStatData;
        }
    }

    public CharacterStat MaxHealth { get; private set; }
    public CharacterStat AttackPower { get; private set; }
    public CharacterStat Defense { get; private set; }
    public CharacterStat MoveSpeed { get; private set; }

    /// <summary>
    /// 런타임 스탯 객체 생성이 완료됐는지 나타냅니다.
    /// </summary>
    public bool IsInitialized
    {
        get
        {
            return MaxHealth != null &&
                   AttackPower != null &&
                   Defense != null &&
                   MoveSpeed != null;
        }
    }

    private void Awake()
    {
        if (_baseStatData == null)
        {
            Debug.LogError(
                $"[{nameof(CharacterAttributes)}] Base stat data is not assigned.",
                this);

            InitializeValues(0f, 0f, 0f, 0f);
            return;
        }

        Initialize(_baseStatData);
    }

    /// <summary>
    /// 지정한 데이터로 공통 캐릭터 스탯을 초기화하거나 기본값을 갱신합니다.
    /// </summary>
    public void Initialize(CharacterBaseStatData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        _baseStatData = data;
        InitializeValues(
            data.MaxHealth,
            data.AttackPower,
            data.Defense,
            data.MoveSpeed);
    }

    /// <summary>
    /// 공통 캐릭터 스탯을 종류로 검색합니다.
    /// </summary>
    public bool TryGetStat(CharacterStatType statType, out CharacterStat stat)
    {
        switch (statType)
        {
            case CharacterStatType.MaxHealth:
                stat = MaxHealth;
                break;

            case CharacterStatType.AttackPower:
                stat = AttackPower;
                break;

            case CharacterStatType.Defense:
                stat = Defense;
                break;

            case CharacterStatType.MoveSpeed:
                stat = MoveSpeed;
                break;

            default:
                stat = null;
                return false;
        }

        return stat != null;
    }

    private void InitializeValues(
        float maxHealth,
        float attackPower,
        float defense,
        float moveSpeed)
    {
        if (!IsInitialized)
        {
            MaxHealth = new CharacterStat(maxHealth);
            AttackPower = new CharacterStat(attackPower);
            Defense = new CharacterStat(defense);
            MoveSpeed = new CharacterStat(moveSpeed);
            return;
        }

        MaxHealth.SetBaseValue(maxHealth);
        AttackPower.SetBaseValue(attackPower);
        Defense.SetBaseValue(defense);
        MoveSpeed.SetBaseValue(moveSpeed);
    }
}
