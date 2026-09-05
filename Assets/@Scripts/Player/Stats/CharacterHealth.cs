using System;
using ProjectG.Combat;
using UnityEngine;

/// <summary>
/// 기존 플레이어와 몬스터 코드가 함께 사용할 수 있는 하위 호환 체력 API입니다.
/// NetworkCharacterHealth가 있으면 서버 권한 체력 컴포넌트를 감싸는 호환 창구로 동작합니다.
/// </summary>
[RequireComponent(typeof(CharacterAttributes))]
public sealed class CharacterHealth : MonoBehaviour
{
    private CharacterAttributes _attributes;
    private NetworkCharacterHealth _networkHealth;
    private float _currentHealth;
    private bool _isInitialized;

    public event Action<float, float> HealthChanged;
    public event Action HealthDepleted;

    public float CurrentHealth => _networkHealth != null ? _networkHealth.CurrentHealth : _currentHealth;

    public float MaxHealth =>
        _attributes != null && _attributes.MaxHealth != null
            ? Mathf.Max(0f, _attributes.MaxHealth.Value)
            : 0f;

    public bool IsInitialized => _networkHealth != null
        ? _attributes != null && _attributes.MaxHealth != null
        : _isInitialized;

    public bool IsDepleted => IsInitialized && CurrentHealth <= 0f;

    /// <summary>
    /// 기존 몬스터와 테스트 코드 호환을 위해 유지하는 단일 인자 체력 변경 이벤트입니다.
    /// </summary>
    public Action<float> onHpChange;

    private void Awake()
    {
        _attributes = GetComponent<CharacterAttributes>();
        _networkHealth = GetComponent<NetworkCharacterHealth>();

        if (_networkHealth != null)
        {
            _networkHealth.HealthChanged += HandleNetworkHealthChanged;
            _networkHealth.HealthDepleted += HandleNetworkHealthDepleted;
            return;
        }

        TryInitialize();
    }

    private void Start()
    {
        if (_networkHealth != null || TryInitialize())
            return;

        Debug.LogError(
            $"[{nameof(CharacterHealth)}] Character attributes are not initialized.",
            this);
    }

    private void OnDestroy()
    {
        if (_networkHealth != null)
        {
            _networkHealth.HealthChanged -= HandleNetworkHealthChanged;
            _networkHealth.HealthDepleted -= HandleNetworkHealthDepleted;
        }

        if (_attributes != null && _attributes.MaxHealth != null)
            _attributes.MaxHealth.ValueChanged -= HandleMaxHealthChanged;
    }

    /// <summary>
    /// 기존 오브젝트에는 로컬 데미지를 적용하고 네트워크 체력이 있으면 서버에서 적용합니다.
    /// </summary>
    public float TakeDamage(float amount)
    {
        if (_networkHealth != null)
            return _networkHealth.ApplyDamageServer(amount);

        if (amount <= 0f || !TryInitialize() || IsDepleted)
            return 0f;

        float previousHealth = _currentHealth;
        SetCurrentHealthInternal(previousHealth - amount);
        return previousHealth - _currentHealth;
    }

    /// <summary>
    /// 기존 오브젝트는 로컬에서 회복하고 네트워크 체력이 있으면 서버에서 회복합니다.
    /// </summary>
    public float Heal(float amount)
    {
        if (_networkHealth != null)
            return _networkHealth.HealServer(amount);

        if (amount <= 0f || !TryInitialize() || IsDepleted)
            return 0f;

        float previousHealth = _currentHealth;
        SetCurrentHealthInternal(previousHealth + amount);
        return _currentHealth - previousHealth;
    }

    /// <summary>
    /// 현재 체력을 지정합니다. 네트워크 오브젝트는 서버에서만 변경할 수 있습니다.
    /// </summary>
    public void SetCurrentHealth(float value)
    {
        if (_networkHealth != null)
        {
            _networkHealth.SetHealthServer(value);
            return;
        }

        if (TryInitialize())
            SetCurrentHealthInternal(value);
    }

    /// <summary>
    /// 현재 최대 체력까지 회복합니다.
    /// </summary>
    public void RestoreToMaximum()
    {
        SetCurrentHealth(MaxHealth);
    }

    private bool TryInitialize()
    {
        if (_isInitialized)
            return true;

        if (_attributes == null)
            _attributes = GetComponent<CharacterAttributes>();

        if (_attributes == null || _attributes.MaxHealth == null)
            return false;

        _attributes.MaxHealth.ValueChanged -= HandleMaxHealthChanged;
        _attributes.MaxHealth.ValueChanged += HandleMaxHealthChanged;

        _currentHealth = MaxHealth;
        _isInitialized = true;
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
        return true;
    }

    private void HandleMaxHealthChanged(float _, float currentMaximum)
    {
        float previousHealth = _currentHealth;
        float clampedMaximum = Mathf.Max(0f, currentMaximum);
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, clampedMaximum);

        HealthChanged?.Invoke(_currentHealth, clampedMaximum);

        if (previousHealth > 0f && _currentHealth <= 0f)
            HealthDepleted?.Invoke();
    }

    private void SetCurrentHealthInternal(float value)
    {
        float previousHealth = _currentHealth;
        float nextHealth = Mathf.Clamp(value, 0f, MaxHealth);

        if (Mathf.Approximately(previousHealth, nextHealth))
            return;

        _currentHealth = nextHealth;
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
        onHpChange?.Invoke(_currentHealth);

        if (previousHealth > 0f && _currentHealth <= 0f)
            HealthDepleted?.Invoke();
    }

    private void HandleNetworkHealthChanged(float currentHealth, float maximumHealth)
    {
        HealthChanged?.Invoke(currentHealth, maximumHealth);
        onHpChange?.Invoke(currentHealth);
    }

    private void HandleNetworkHealthDepleted()
    {
        HealthDepleted?.Invoke();
    }
}
