using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 네트워크 플레이어와 몬스터가 공통으로 사용하는 서버 권한 체력입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterAttributes))]
    public sealed class NetworkCharacterHealth : NetworkBehaviour
    {
        [SerializeField] private bool _initializeAtMaximum = true;

        private readonly NetworkVariable<float> _currentHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private CharacterAttributes _attributes;
        private float _offlineHealth;

        /// <summary>현재 체력과 최대 체력이 변경됐을 때 발생합니다.</summary>
        public event Action<float, float> HealthChanged;

        /// <summary>체력이 처음으로 0 이하가 됐을 때 발생합니다.</summary>
        public event Action HealthDepleted;

        /// <summary>네트워크 상태에 맞는 현재 체력을 반환합니다.</summary>
        public float CurrentHealth
        {
            get
            {
                if (IsSpawned)
                    return _currentHealth.Value;

                return _offlineHealth;
            }
        }

        /// <summary>CharacterAttributes에 설정된 최대 체력을 반환합니다.</summary>
        public float MaxHealth
        {
            get
            {
                if (_attributes == null)
                    return 0f;

                CharacterStat maximumHealthStat = _attributes.MaxHealth;
                if (maximumHealthStat == null)
                    return 0f;

                return Mathf.Max(0f, maximumHealthStat.Value);
            }
        }

        /// <summary>현재 체력이 0 이하인지 여부를 반환합니다.</summary>
        public bool IsDepleted
        {
            get
            {
                return CurrentHealth <= 0f;
            }
        }

        private void Awake()
        {
            CacheDependencies();
            InitializeOfflineHealth();
        }

        private void OnEnable()
        {
            SubscribeMaximumHealth();
        }

        private void Start()
        {
            RestoreOfflineHealthIfNeeded();
        }

        private void OnDisable()
        {
            UnsubscribeMaximumHealth();
        }

        /// <summary>네트워크 스폰 시 서버 체력을 초기화하고 변경 이벤트 구독을 시작합니다.</summary>
        public override void OnNetworkSpawn()
        {
            _currentHealth.OnValueChanged += HandleNetworkHealthChanged;

            if (IsServer)
                InitializeNetworkHealth();

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>네트워크 체력을 오프라인 값으로 보관하고 변경 이벤트 구독을 해제합니다.</summary>
        public override void OnNetworkDespawn()
        {
            _offlineHealth = _currentHealth.Value;
            _currentHealth.OnValueChanged -= HandleNetworkHealthChanged;
        }

        /// <summary>
        /// 서버 또는 오프라인 실행 환경에서 최종 데미지를 적용합니다.
        /// 실제로 감소한 체력을 반환합니다.
        /// </summary>
        public float ApplyDamageServer(float amount)
        {
            if (amount <= 0f)
                return 0f;

            if (!HasServerAuthority())
                return 0f;

            if (IsDepleted)
                return 0f;

            float previousHealth = CurrentHealth;
            float requestedHealth = previousHealth - amount;

            SetHealthServer(requestedHealth);
            return previousHealth - CurrentHealth;
        }

        /// <summary>
        /// 서버 또는 오프라인 실행 환경에서 체력을 회복합니다.
        /// 실제로 증가한 체력을 반환합니다.
        /// </summary>
        public float HealServer(float amount)
        {
            if (amount <= 0f)
                return 0f;

            if (!HasServerAuthority())
                return 0f;

            if (IsDepleted)
                return 0f;

            float previousHealth = CurrentHealth;
            float requestedHealth = previousHealth + amount;

            SetHealthServer(requestedHealth);
            return CurrentHealth - previousHealth;
        }

        /// <summary>
        /// 서버에서 체력을 지정합니다.
        /// 스폰, 저장 데이터 복구, 기존 시스템 호환 코드에서 사용합니다.
        /// </summary>
        public void SetHealthServer(float value)
        {
            if (!HasServerAuthority())
                return;

            float clampedHealth = Mathf.Clamp(value, 0f, MaxHealth);

            if (IsSpawned)
            {
                SetNetworkHealth(clampedHealth);
                return;
            }

            SetOfflineHealth(clampedHealth);
        }

        private void CacheDependencies()
        {
            _attributes = GetComponent<CharacterAttributes>();
        }

        private void InitializeOfflineHealth()
        {
            if (_initializeAtMaximum)
            {
                _offlineHealth = MaxHealth;
                return;
            }

            _offlineHealth = 0f;
        }

        private void RestoreOfflineHealthIfNeeded()
        {
            if (IsSpawned)
                return;

            if (!_initializeAtMaximum)
                return;

            if (_offlineHealth > 0f)
                return;

            float maximumHealth = MaxHealth;
            if (maximumHealth <= 0f)
                return;

            SetOfflineHealth(maximumHealth);
        }

        private void InitializeNetworkHealth()
        {
            float initialHealth = _offlineHealth;
            if (_initializeAtMaximum)
                initialHealth = MaxHealth;

            _currentHealth.Value = Mathf.Clamp(initialHealth, 0f, MaxHealth);
        }

        private void SetNetworkHealth(float health)
        {
            if (Mathf.Approximately(_currentHealth.Value, health))
                return;

            _currentHealth.Value = health;
        }

        private void SetOfflineHealth(float health)
        {
            float previousHealth = _offlineHealth;
            if (Mathf.Approximately(previousHealth, health))
                return;

            _offlineHealth = health;
            RaiseHealthEvents(previousHealth, health);
        }

        private void SubscribeMaximumHealth()
        {
            if (_attributes == null)
                CacheDependencies();

            CharacterStat maximumHealthStat = GetMaximumHealthStat();
            if (maximumHealthStat == null)
                return;

            maximumHealthStat.ValueChanged -= HandleMaximumHealthChanged;
            maximumHealthStat.ValueChanged += HandleMaximumHealthChanged;
        }

        private void UnsubscribeMaximumHealth()
        {
            CharacterStat maximumHealthStat = GetMaximumHealthStat();
            if (maximumHealthStat == null)
                return;

            maximumHealthStat.ValueChanged -= HandleMaximumHealthChanged;
        }

        private CharacterStat GetMaximumHealthStat()
        {
            if (_attributes == null)
                return null;

            return _attributes.MaxHealth;
        }

        private void HandleMaximumHealthChanged(float previousMaximum, float currentMaximum)
        {
            float safeMaximumHealth = Mathf.Max(0f, currentMaximum);

            if (HasServerAuthority())
            {
                float clampedCurrentHealth = Mathf.Min(CurrentHealth, safeMaximumHealth);
                SetHealthServer(clampedCurrentHealth);
            }

            HealthChanged?.Invoke(CurrentHealth, safeMaximumHealth);
        }

        private void HandleNetworkHealthChanged(float previousHealth, float currentHealth)
        {
            RaiseHealthEvents(previousHealth, currentHealth);
        }

        private void RaiseHealthEvents(float previousHealth, float currentHealth)
        {
            HealthChanged?.Invoke(currentHealth, MaxHealth);

            bool becameDepleted = previousHealth > 0f && currentHealth <= 0f;
            if (becameDepleted)
                HealthDepleted?.Invoke();
        }

        private bool HasServerAuthority()
        {
            if (IsSpawned)
                return IsServer;

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                return true;

            return !networkManager.IsListening;
        }
    }
}
