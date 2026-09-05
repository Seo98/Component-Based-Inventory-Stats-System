using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// CharacterController를 사용하여 로컬 이동, 중력, 점프 속도를 적용합니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterAttributes))]
public sealed class PlayerMotor : MonoBehaviour
{
    [Header("수직 이동")]
    [SerializeField, Min(0f)] private float _gravity = 20f;
    [SerializeField, Min(0f)] private float _groundedForce = 2f;

    private CharacterController _controller;
    private CharacterAttributes _attributes;
    private NetworkObject _networkObject;
    private Vector2 _moveInput;
    private float _verticalVelocity;

    /// <summary>
    /// 요청된 이동 입력이 변경되면 발생합니다.
    /// </summary>
    public event Action<Vector2> MoveInputChanged;

    /// <summary>
    /// 최근에 전달된 정규화 이동 입력입니다.
    /// </summary>
    public Vector2 MoveInput => _moveInput;

    /// <summary>
    /// 현재 이동 입력이 있는지 반환합니다.
    /// </summary>
    public bool IsMoving => _moveInput.sqrMagnitude > 0f;

    /// <summary>
    /// CharacterController의 지면 접촉 상태를 반환합니다.
    /// </summary>
    public bool IsGrounded => _controller != null && _controller.isGrounded;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _attributes = GetComponent<CharacterAttributes>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        if (!CanMoveLocally())
            return;

        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -_groundedForce;

        Vector3 direction = transform.right * _moveInput.x +
                            transform.forward * _moveInput.y;
        direction = Vector3.ClampMagnitude(direction, 1f);
        float moveSpeed = _attributes.MoveSpeed != null
            ? Mathf.Max(0f, _attributes.MoveSpeed.Value)
            : 0f;

        _verticalVelocity -= _gravity * Time.deltaTime;

        Vector3 velocity = direction * moveSpeed;
        velocity.y = _verticalVelocity;

        CollisionFlags collisionFlags = _controller.Move(velocity * Time.deltaTime);

        if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    private void OnDisable()
    {
        _moveInput = Vector2.zero;
        _verticalVelocity = 0f;
    }

    /// <summary>
    /// 원하는 수평 이동 방향을 갱신합니다.
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        if (!CanMoveLocally())
            return;

        Vector2 nextInput = Vector2.ClampMagnitude(input, 1f);

        if (_moveInput == nextInput)
            return;

        bool wasMoving = IsMoving;
        _moveInput = nextInput;

        if (wasMoving != IsMoving)
        {
            Debug.Log(
                IsMoving
                    ? "[PlayerMotor] 이동 시작."
                    : "[PlayerMotor] 이동 정지.",
                this);
        }

        MoveInputChanged?.Invoke(_moveInput);
    }

    /// <summary>
    /// 로컬에서 조작 중이고 지면에 있을 때 점프 속도를 적용합니다.
    /// </summary>
    public bool TryJump(float jumpHeight)
    {
        if (!CanMoveLocally() || !IsGrounded || jumpHeight <= 0f || _gravity <= 0f)
            return false;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * _gravity);
        return true;
    }

    private bool CanMoveLocally()
    {
        return _networkObject == null ||
               !_networkObject.IsSpawned ||
               _networkObject.IsOwner;
    }
}
