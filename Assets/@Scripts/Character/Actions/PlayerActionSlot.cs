namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 플레이어 입력과 UI가 캐릭터의 행동 칸을 선택할 때 사용하는 네트워크 식별자입니다.
    /// 구체적인 공격 이름이나 히트박스 모양은 이 열거형에 추가하지 않습니다.
    /// </summary>
    public enum PlayerActionSlot : ushort
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
        Mobility = 3,
        Skill1 = 10,
        Skill2 = 11,
        Ultimate = 20
    }
}
