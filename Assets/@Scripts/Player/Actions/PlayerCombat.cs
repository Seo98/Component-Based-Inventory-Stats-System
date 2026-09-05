using System;
using ProjectG.CharacterActions;
using UnityEngine;

/// <summary>
/// 플레이어 공격 명령을 공통 캐릭터 행동 요청으로 변환합니다.
/// </summary>
[RequireComponent(typeof(CharacterAttributes))]
public sealed class PlayerCombat : MonoBehaviour
{
    private CharacterAttributes _attributes;
    private CharacterActionController _actionController;

    /// <summary>
    /// 기본 공격 요청이 접수되면 발생합니다.
    /// 애니메이션이나 효과 표시 컴포넌트가 구독할 수 있습니다.
    /// </summary>
    public event Action PrimaryAttackRequested;

    private void Awake()
    {
        _attributes = GetComponent<CharacterAttributes>();
        _actionController = GetComponent<CharacterActionController>();
    }

    /// <summary>
    /// 서버 권한 기본 공격을 요청합니다.
    /// </summary>
    public void RequestPrimaryAttack()
    {
        if (_actionController == null)
        {
            float attackPower = _attributes.AttackPower != null
                ? _attributes.AttackPower.Value
                : 0f;

            Debug.LogWarning(
                $"[{nameof(PlayerCombat)}] CharacterActionController is missing. AttackPower={attackPower:0.##}",
                this);
            return;
        }

        bool accepted = _actionController.RequestAction(
            PlayerActionSlot.Primary,
            transform.position + transform.forward,
            transform.forward);

        if (accepted)
            PrimaryAttackRequested?.Invoke();
    }
}
