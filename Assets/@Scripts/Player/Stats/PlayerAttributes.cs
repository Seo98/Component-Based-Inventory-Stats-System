using System;
using UnityEngine;

/// <summary>
/// 힘, 민첩, 지능 등 플레이어 전용 런타임 능력치를 소유합니다.
/// </summary>
[RequireComponent(typeof(CharacterAttributes))]
public sealed class PlayerAttributes : MonoBehaviour
{
    public CharacterAttributes CommonAttributes { get; private set; }

    /// <summary>CharacterAttributes에 연결된 플레이어 전용 원본 데이터입니다.</summary>
    public PlayerBaseStatData BaseStatData
    {
        get
        {
            if (CommonAttributes == null)
                return null;

            return CommonAttributes.BaseStatData as PlayerBaseStatData;
        }
    }

    public CharacterStat MaxStamina { get; private set; }
    public CharacterStat Strength { get; private set; }
    public CharacterStat Agility { get; private set; }
    public CharacterStat Intelligence { get; private set; }
    public CharacterStat Aggro { get; private set; }
    public CharacterStat Stealth { get; private set; }
    public CharacterStat InteractionSpeed { get; private set; }

    /// <summary>
    /// 플레이어 전용 런타임 능력치 생성이 완료됐는지 나타냅니다.
    /// </summary>
    public bool IsInitialized
    {
        get
        {
            return MaxStamina != null &&
                   Strength != null &&
                   Agility != null &&
                   Intelligence != null &&
                   Aggro != null &&
                   Stealth != null &&
                   InteractionSpeed != null;
        }
    }

    private void Awake()
    {
        CommonAttributes = GetComponent<CharacterAttributes>();

        PlayerBaseStatData baseStatData = BaseStatData;
        if (baseStatData == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerAttributes)}] CharacterAttributes must reference a PlayerBaseStatData asset.",
                this);

            InitializeValues(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            return;
        }

        Initialize(baseStatData);
    }

    /// <summary>
    /// 지정한 데이터로 플레이어 전용 능력치를 초기화하거나 기본값을 갱신합니다.
    /// </summary>
    public void Initialize(PlayerBaseStatData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (CommonAttributes == null)
            CommonAttributes = GetComponent<CharacterAttributes>();

        CommonAttributes.Initialize(data);

        InitializeValues(
            data.MaxStamina,
            data.Strength,
            data.Agility,
            data.Intelligence,
            data.Aggro,
            data.Stealth,
            data.InteractionSpeed);
    }

    /// <summary>
    /// 플레이어 전용 및 공통 캐릭터 스탯을 종류로 검색합니다.
    /// </summary>
    public bool TryGetStat(CharacterStatType statType, out CharacterStat stat)
    {
        switch (statType)
        {
            case CharacterStatType.MaxStamina:
                stat = MaxStamina;
                break;

            case CharacterStatType.Strength:
                stat = Strength;
                break;

            case CharacterStatType.Agility:
                stat = Agility;
                break;

            case CharacterStatType.Intelligence:
                stat = Intelligence;
                break;

            case CharacterStatType.Aggro:
                stat = Aggro;
                break;

            case CharacterStatType.Stealth:
                stat = Stealth;
                break;

            case CharacterStatType.InteractionSpeed:
                stat = InteractionSpeed;
                break;

            default:
                if (CommonAttributes != null)
                    return CommonAttributes.TryGetStat(statType, out stat);

                stat = null;
                return false;
        }

        return stat != null;
    }

    private void InitializeValues(
        float maxStamina,
        float strength,
        float agility,
        float intelligence,
        float aggro,
        float stealth,
        float interactionSpeed)
    {
        if (!IsInitialized)
        {
            MaxStamina = new CharacterStat(maxStamina);
            Strength = new CharacterStat(strength);
            Agility = new CharacterStat(agility);
            Intelligence = new CharacterStat(intelligence);
            Aggro = new CharacterStat(aggro);
            Stealth = new CharacterStat(stealth);
            InteractionSpeed = new CharacterStat(interactionSpeed);
            return;
        }

        MaxStamina.SetBaseValue(maxStamina);
        Strength.SetBaseValue(strength);
        Agility.SetBaseValue(agility);
        Intelligence.SetBaseValue(intelligence);
        Aggro.SetBaseValue(aggro);
        Stealth.SetBaseValue(stealth);
        InteractionSpeed.SetBaseValue(interactionSpeed);
    }
}
