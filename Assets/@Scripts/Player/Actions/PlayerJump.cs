using System;
using UnityEngine;

/// <summary>
/// 점프 가능 여부와 스태미나를 확인하고 수직 이동을 PlayerMotor에 요청합니다.
/// </summary>
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerStamina))]
public sealed class PlayerJump : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _jumpHeight = 1.5f;
    [SerializeField, Min(0f)] private float _staminaCost = 5f;

    private PlayerMotor _motor;
    private PlayerStamina _stamina;

    /// <summary>
    /// 점프가 정상적으로 시작되면 발생합니다.
    /// </summary>
    public event Action JumpStarted;

    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _stamina = GetComponent<PlayerStamina>();
    }

    /// <summary>
    /// 지면에 있고 스태미나가 충분하면 점프를 시작합니다.
    /// </summary>
    public void RequestJump()
    {
        if (_staminaCost > 0f && !_stamina.CanConsume(_staminaCost))
        {
            Debug.Log("[PlayerJump] 스태미나가 부족하여 점프할 수 없습니다.", this);
            return;
        }

        if (!_motor.TryJump(_jumpHeight))
            return;

        if (_staminaCost > 0f)
            _stamina.TryConsume(_staminaCost);

        Debug.Log(
            $"[PlayerJump] 점프 시작. Height={_jumpHeight:0.##}, StaminaCost={_staminaCost:0.##}",
            this);

        JumpStarted?.Invoke();
    }
}
