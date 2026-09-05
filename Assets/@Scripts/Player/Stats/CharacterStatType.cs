/// <summary>
/// 장비나 버프 데이터가 대상으로 지정할 수 있는 런타임 스탯입니다.
/// 에셋 직렬화 안정성을 위해 각 항목의 숫자값을 변경하지 않습니다.
/// </summary>
public enum CharacterStatType
{
    None = 0,

    MaxHealth = 5,
    AttackPower = 10,
    Defense = 20,
    MoveSpeed = 30,

    MaxStamina = 90,
    Strength = 100,
    Agility = 110,
    Intelligence = 120,
    Aggro = 130,
    Stealth = 140,
    InteractionSpeed = 150,

    //몬스터 추가 능력치
    DangerLevel=300,
    ActionDelay=310,
    SensorDistance=320
}
