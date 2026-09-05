using ProjectG.CharacterActions;
using ProjectG.Combat;
using Unity.Netcode;
using UnityEngine;

namespace ProjectG.CharacterSamples
{
    /// <summary>
    /// 이동 없이 주변 플레이어를 바라보고 근접 Action SO 하나만 요청하는 서버 전용 샘플 AI입니다.
    /// 기존 몬스터 체력이나 SMB 어댑터에 의존하지 않고 공통 DamageReceiver를 직접 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterActionController))]
    [RequireComponent(typeof(CharacterTeam))]
    public sealed class ServerSimpleMeleeMonsterAI : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterActionController _actionController;
        [SerializeField] private HitboxActionDefinition _meleeAction;

        [Header("Target Search")]
        [SerializeField, Min(0)] private int _targetTeamId = 1;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField, Min(0.1f)] private float _detectionRadius = 5f;
        [SerializeField, Range(1, 64)] private int _targetBufferCapacity = 16;
        [SerializeField, Min(0.02f)] private float _targetSearchInterval = 0.25f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] private float _attackDistance = 2.2f;
        [SerializeField, Range(0f, 180f)] private float _maximumAttackAngle = 12f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 360f;
        [SerializeField, Min(0.02f)] private float _attackRetryInterval = 0.1f;

        private Collider[] _targetBuffer;
        private DamageReceiver _target;
        private NetworkCharacterHealth _health;
        private CharacterTeam _team;
        private double _nextTargetSearchAt;
        private double _nextAttackAttemptAt;

        /// <summary>현재 공격 대상으로 선택된 공통 데미지 수신기입니다.</summary>
        public DamageReceiver CurrentTarget => _target;

        private void Awake()
        {
            if (_actionController == null)
                _actionController = GetComponent<CharacterActionController>();

            _health = GetComponent<NetworkCharacterHealth>();
            _team = GetComponent<CharacterTeam>();
            _targetBuffer = new Collider[Mathf.Max(1, _targetBufferCapacity)];
        }

        private void Update()
        {
            if (!HasServerAuthority() || (_health != null && _health.IsDepleted))
                return;

            double now = GetServerTime();
            if (!IsTargetValid(_target) || !IsInsideDetectionRadius(_target))
                _target = null;

            if (_target == null && now >= _nextTargetSearchAt)
            {
                _nextTargetSearchAt = now + _targetSearchInterval;
                _target = FindNearestTarget();
            }

            if (_target == null)
                return;

            Vector3 targetDirection = _target.DamageTransform.position - transform.position;
            Vector3 planarDirection = targetDirection;
            planarDirection.y = 0f;

            if (planarDirection.sqrMagnitude > Mathf.Epsilon)
                RotateTowards(planarDirection, Time.deltaTime);

            if (now < _nextAttackAttemptAt)
                return;

            _nextAttackAttemptAt = now + _attackRetryInterval;
            if (planarDirection.sqrMagnitude > _attackDistance * _attackDistance)
                return;

            if (Vector3.Angle(transform.forward, planarDirection) > _maximumAttackAngle)
                return;

            _actionController.TryRequestActionFromServer(
                _meleeAction,
                _target.DamageTransform.position,
                targetDirection);
        }

        /// <summary>
        /// 서버 AI나 테스트 코드가 자동 탐색 대신 공격 대상을 직접 지정합니다.
        /// 팀이 다르며 살아 있는 DamageReceiver만 허용합니다.
        /// </summary>
        public bool TrySetTarget(DamageReceiver target)
        {
            if (!HasServerAuthority() || !IsTargetValid(target))
                return false;

            _target = target;
            return true;
        }

        /// <summary>현재 공격 대상을 해제합니다.</summary>
        public void ClearTarget()
        {
            if (HasServerAuthority())
                _target = null;
        }

        private DamageReceiver FindNearestTarget()
        {
            if (_targetBuffer == null || _targetBuffer.Length == 0)
                _targetBuffer = new Collider[Mathf.Max(1, _targetBufferCapacity)];

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _detectionRadius,
                _targetBuffer,
                _targetLayers,
                QueryTriggerInteraction.Ignore);

            DamageReceiver nearest = null;
            float nearestSqrDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = _targetBuffer[i];
                _targetBuffer[i] = null;
                if (candidateCollider == null)
                    continue;

                DamageReceiver candidate =
                    candidateCollider.GetComponentInParent<DamageReceiver>();
                if (!IsTargetValid(candidate))
                    continue;

                Vector3 offset = candidate.DamageTransform.position - transform.position;
                offset.y = 0f;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance)
                    continue;

                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }

        private bool IsTargetValid(DamageReceiver candidate)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsAlive)
                return false;

            CharacterTeam candidateTeam = candidate.GetComponent<CharacterTeam>();
            if (candidateTeam == null || candidateTeam.TeamId != _targetTeamId)
                return false;

            return _team == null || candidateTeam.TeamId != _team.TeamId;
        }

        private bool IsInsideDetectionRadius(DamageReceiver candidate)
        {
            if (candidate == null)
                return false;

            Vector3 offset = candidate.DamageTransform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _detectionRadius * _detectionRadius;
        }

        private void RotateTowards(Vector3 planarDirection, float deltaTime)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * deltaTime);
        }

        private bool HasServerAuthority()
        {
            if (IsSpawned)
                return IsServer;

            return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _attackDistance);
            Gizmos.color = previousColor;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_actionController == null)
                _actionController = GetComponent<CharacterActionController>();

            _detectionRadius = Mathf.Max(0.1f, _detectionRadius);
            _attackDistance = Mathf.Clamp(_attackDistance, 0.1f, _detectionRadius);
            _targetBufferCapacity = Mathf.Clamp(_targetBufferCapacity, 1, 64);
        }
#endif
    }
}
