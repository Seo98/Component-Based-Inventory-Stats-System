using System;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 이전 코드 호환용 식별자입니다. 새 플레이어 코드는 <see cref="PlayerActionSlot"/>을 사용하고,
    /// 서버 AI는 등록된 <see cref="CharacterActionDefinition"/>을 직접 요청하세요.
    /// </summary>
    [Obsolete("플레이어는 PlayerActionSlot을 사용하고 서버 AI는 Action SO를 직접 요청하세요.")]
    public enum CharacterActionId : ushort
    {
        None = 0,
        PrimaryAttack = 1,
        SecondaryAttack = 2,
        Roll = 3,
        Skill1 = 10,
        Skill2 = 11
    }
}
