using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어의 현재 스태미나, 소비 및 자동 회복을 관리합니다.
/// 최대 스태미나는 <see cref="PlayerAttributes"/>의 최종 스탯 값을 사용합니다.
/// </summary>
[RequireComponent(typeof(PlayerAttributes))]
public sealed class PlayerStamina : MonoBehaviour
{
    [Header("Recovery")]
    [SerializeField, Min(0f)] private float _recoveryPerSecond = 5f;
    [SerializeField, Min(0f)] private float _recoveryDelay = 1f;

    private PlayerAttributes _attributes;
    private NetworkObject _networkObject;
    private float _currentStamina;
    private float _recoveryResumeTime;
    private bool _isInitialized;

    /// <summary>
    /// 현재 또는 최대 스태미나가 변경될 때 현재값과 최대값을 전달합니다.
    /// </summary>
    public event Action<float, float> StaminaChanged;

    /// <summary>
    /// 스태미나가 0보다 큰 값에서 0으로 변경될 때 발생합니다.
    /// </summary>
    public event Action StaminaDepleted;

    /// <summary>
    /// 현재 스태미나입니다.
    /// </summary>
    public float CurrentStamina => _currentStamina;

    /// <summary>
    /// 장비와 버프를 포함한 최종 최대 스태미나입니다.
    /// </summary>
    public float MaxStamina =>
        _attributes != null && _attributes.MaxStamina != null
            ? Mathf.Max(0f, _attributes.MaxStamina.Value)
            : 0f;

    /// <summary>
    /// 스태미나 객체가 초기화되었는지 나타냅니다.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    private void Awake()
    {
        _attributes = GetComponent<PlayerAttributes>();
        _networkObject = GetComponent<NetworkObject>();
        TryInitialize();
    }

    private void Start()
    {
        if (TryInitialize())
            return;

        Debug.LogError(
            $"[{nameof(PlayerStamina)}] Player attributes are not initialized.",
            this);
    }

    private void Update()
    {
        if (!CanSimulateLocally() || !TryInitialize())
            return;

        if (Time.time < _recoveryResumeTime || _currentStamina >= MaxStamina)
            return;

        Restore(_recoveryPerSecond * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (_attributes != null && _attributes.MaxStamina != null)
            _attributes.MaxStamina.ValueChanged -= HandleMaxStaminaChanged;
    }

    /// <summary>
    /// 지정한 스태미나를 소비할 수 있는지 확인합니다.
    /// </summary>
    public bool CanConsume(float amount)
    {
        return amount > 0f && TryInitialize() && _currentStamina >= amount;
    }

    /// <summary>
    /// 스태미나가 충분하면 지정한 값을 소비하고 true를 반환합니다.
    /// 소비 후 일정 시간 동안 자동 회복이 지연됩니다.
    /// </summary>
    public bool TryConsume(float amount)
    {
        if (!CanConsume(amount))
            return false;

        _recoveryResumeTime = Time.time + _recoveryDelay;
        SetCurrentStaminaInternal(_currentStamina - amount);
        return true;
    }

    /// <summary>
    /// 스태미나를 회복하고 실제로 증가한 값을 반환합니다.
    /// </summary>
    public float Restore(float amount)
    {
        if (amount <= 0f || !TryInitialize())
            return 0f;

        float previousStamina = _currentStamina;
        SetCurrentStaminaInternal(previousStamina + amount);
        return _currentStamina - previousStamina;
    }

    /// <summary>
    /// 저장 데이터 복구 또는 네트워크 동기화를 위해 현재 스태미나를 지정합니다.
    /// </summary>
    public void SetCurrentStamina(float value)
    {
        if (!TryInitialize())
            return;

        SetCurrentStaminaInternal(value);
    }

    /// <summary>
    /// 현재 스태미나를 최종 최대 스태미나까지 회복합니다.
    /// </summary>
    public void RestoreToMaximum()
    {
        if (!TryInitialize())
            return;

        SetCurrentStaminaInternal(MaxStamina);
    }

    private bool TryInitialize()
    {
        if (_isInitialized)
            return true;

        if (_attributes == null)
            _attributes = GetComponent<PlayerAttributes>();

        if (_attributes == null || _attributes.MaxStamina == null)
            return false;

        _attributes.MaxStamina.ValueChanged -= HandleMaxStaminaChanged;
        _attributes.MaxStamina.ValueChanged += HandleMaxStaminaChanged;

        _currentStamina = MaxStamina;
        _isInitialized = true;
        StaminaChanged?.Invoke(_currentStamina, MaxStamina);
        return true;
    }

    private bool CanSimulateLocally()
    {
        return _networkObject == null ||
               !_networkObject.IsSpawned ||
               _networkObject.IsOwner;
    }

    private void HandleMaxStaminaChanged(float _, float currentMaximum)
    {
        float previousStamina = _currentStamina;
        float clampedMaximum = Mathf.Max(0f, currentMaximum);
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, clampedMaximum);

        StaminaChanged?.Invoke(_currentStamina, clampedMaximum);

        if (previousStamina > 0f && _currentStamina <= 0f)
            StaminaDepleted?.Invoke();
    }

    private void SetCurrentStaminaInternal(float value)
    {
        float previousStamina = _currentStamina;
        float nextStamina = Mathf.Clamp(value, 0f, MaxStamina);

        if (previousStamina.Equals(nextStamina))
            return;

        _currentStamina = nextStamina;
        StaminaChanged?.Invoke(_currentStamina, MaxStamina);

        if (previousStamina > 0f && _currentStamina <= 0f)
            StaminaDepleted?.Invoke();
    }
}
