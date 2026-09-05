using ProjectG.CharacterActions;
using ProjectG.Combat;
using UnityEngine;

namespace ProjectG.Debugging
{
    /// <summary>
    /// 캐릭터의 공격 판정과 체력 변화를 Console에 표시하는 테스트 전용 컴포넌트입니다.
    /// Editor와 Development Build에서만 이벤트를 구독합니다.
    /// </summary>
    [AddComponentMenu("Project G/Debug/Combat Debug Logger")]
    [DisallowMultipleComponent]
    public sealed class CombatDebugLogger : MonoBehaviour
    {
        [Header("Log Categories")]
        [SerializeField] private bool _logActionFlow = true;
        [SerializeField] private bool _logHitResult = true;
        [SerializeField] private bool _logDamage = true;
        [SerializeField] private bool _logHealth = true;

        private PlayerCombat _playerCombat;
        private CharacterActionController _actionController;
        private DamageReceiver _damageReceiver;
        private NetworkCharacterHealth _health;

        private void Awake()
        {
            CacheDependencies();
        }

        private void OnEnable()
        {
            if (!IsLoggingAvailable())
                return;

            CacheDependencies();
            SubscribeEvents();
        }

        private void Start()
        {
            if (!IsLoggingAvailable() || !_logHealth || _health == null)
                return;

            Debug.Log(
                $"[CombatDebug][{name}] 초기 체력: {_health.CurrentHealth:0.##}/{_health.MaxHealth:0.##}",
                this);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void CacheDependencies()
        {
            _playerCombat = GetComponent<PlayerCombat>();
            _actionController = GetComponent<CharacterActionController>();
            _damageReceiver = GetComponent<DamageReceiver>();
            _health = GetComponent<NetworkCharacterHealth>();
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents();

            if (_playerCombat != null)
                _playerCombat.PrimaryAttackRequested += HandlePrimaryAttackRequested;

            if (_actionController != null)
            {
                _actionController.ActionStarted += HandleActionStarted;
                _actionController.ActionExecuted += HandleActionExecuted;
                _actionController.HitboxResolved += HandleHitboxResolved;
            }

            if (_damageReceiver != null)
                _damageReceiver.DamageReceived += HandleDamageReceived;

            if (_health != null)
                _health.HealthChanged += HandleHealthChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_playerCombat != null)
                _playerCombat.PrimaryAttackRequested -= HandlePrimaryAttackRequested;

            if (_actionController != null)
            {
                _actionController.ActionStarted -= HandleActionStarted;
                _actionController.ActionExecuted -= HandleActionExecuted;
                _actionController.HitboxResolved -= HandleHitboxResolved;
            }

            if (_damageReceiver != null)
                _damageReceiver.DamageReceived -= HandleDamageReceived;

            if (_health != null)
                _health.HealthChanged -= HandleHealthChanged;
        }

        private void HandlePrimaryAttackRequested()
        {
            if (!_logActionFlow)
                return;

            Debug.Log($"[CombatDebug][{name}] 기본 공격 요청 전달", this);
        }

        private void HandleActionStarted(CharacterActionDefinition definition)
        {
            if (!_logActionFlow)
                return;

            Debug.Log(
                $"[CombatDebug][{name}] 공격 승인: {GetActionName(definition)}",
                this);
        }

        private void HandleActionExecuted(CharacterActionDefinition definition)
        {
            if (!_logActionFlow)
                return;

            Debug.Log(
                $"[CombatDebug][{name}] 공격 실행: {GetActionName(definition)}",
                this);
        }

        private void HandleHitboxResolved(
            HitboxActionDefinition definition,
            int damagedTargetCount)
        {
            if (!_logHitResult)
                return;

            string actionName = GetActionName(definition);
            if (damagedTargetCount > 0)
            {
                Debug.Log(
                    $"[CombatDebug][{name}] 공격 성공: {actionName}, 적중 대상 {damagedTargetCount}개",
                    this);
                return;
            }

            Debug.LogWarning(
                $"[CombatDebug][{name}] 공격 실패: {actionName}, 적중 대상 없음",
                this);
        }

        private void HandleDamageReceived(DamageInfo damageInfo, float appliedDamage)
        {
            if (!_logDamage)
                return;

            float currentHealth = _health != null ? _health.CurrentHealth : 0f;
            float maximumHealth = _health != null ? _health.MaxHealth : 0f;

            Debug.Log(
                $"[CombatDebug][{name}] 피격: {appliedDamage:0.##} 데미지, " +
                $"남은 체력 {currentHealth:0.##}/{maximumHealth:0.##}, " +
                $"공격자 NetworkObjectId {damageInfo.SourceNetworkObjectId}",
                this);
        }

        private void HandleHealthChanged(float currentHealth, float maximumHealth)
        {
            if (!_logHealth)
                return;

            Debug.Log(
                $"[CombatDebug][{name}] 체력 변경: {currentHealth:0.##}/{maximumHealth:0.##}",
                this);
        }

        private static string GetActionName(CharacterActionDefinition definition)
        {
            return definition != null ? definition.name : "Unknown Action";
        }

        private static bool IsLoggingAvailable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
}
