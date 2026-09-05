using System;
using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 공통 데미지 수신 컴포넌트입니다.
    /// 방어력을 적용한 최종 데미지를 네트워크 체력에 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkCharacterHealth))]
    public sealed class DamageReceiver : MonoBehaviour, IDamageable
    {
        private NetworkCharacterHealth _health;
        private CharacterAttributes _attributes;
        private CharacterTeam _team;

        public event Action<DamageInfo, float> DamageReceived;

        public Transform DamageTransform => transform;
        public int TeamId => _team != null ? _team.TeamId : 0;
        public bool IsAlive => _health != null && !_health.IsDepleted;

        private void Awake()
        {
            _health = GetComponent<NetworkCharacterHealth>();
            _attributes = GetComponent<CharacterAttributes>();
            _team = GetComponent<CharacterTeam>();
        }

        /// <summary>
        /// 데미지 정보를 검증하고 방어력을 계산한 뒤 서버 체력에 적용합니다.
        /// </summary>
        public bool TryReceiveDamage(in DamageInfo damageInfo)
        {
            if (_health == null || _health.IsDepleted)
                return false;

            float defense =
                _attributes != null && _attributes.Defense != null
                    ? Mathf.Max(0f, _attributes.Defense.Value)
                    : 0f;

            float finalDamage = Mathf.Max(
                damageInfo.MinimumDamage,
                damageInfo.RawDamage - defense);

            float appliedDamage = _health.ApplyDamageServer(finalDamage);
            if (appliedDamage <= 0f)
                return false;

            DamageReceived?.Invoke(damageInfo, appliedDamage);
            return true;
        }
    }
}
