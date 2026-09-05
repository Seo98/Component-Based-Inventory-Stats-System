using System;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 하나 이상의 서버 히트 볼륨으로 대상을 찾아 데미지를 주는 범용 행동 데이터입니다.
    /// 근접 공격뿐 아니라 캐릭터 중심 광역기, 돌진 판정 등에도 사용할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HitboxActionDefinition",
        menuName = "Project G/Character Actions/Hitbox Action")]
    public class HitboxActionDefinition : CharacterActionDefinition
    {
        [Header("Damage")]
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float _minimumDamage = 1f;
        [SerializeField] private bool _allowFriendlyFire;

        [Header("Server Hit Volumes")]
        [SerializeField] private HitVolumeDefinition[] _hitVolumes = Array.Empty<HitVolumeDefinition>();
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Ignore;

        /// <summary>공격자의 AttackPower에 곱할 배율입니다.</summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>방어 계산 뒤 보장할 최소 데미지입니다.</summary>
        public float MinimumDamage => _minimumDamage;

        /// <summary>같은 팀도 공격할 수 있는지 여부입니다.</summary>
        public bool AllowFriendlyFire => _allowFriendlyFire;

        /// <summary>물리 판정에 포함할 레이어입니다.</summary>
        public LayerMask TargetLayers => _targetLayers;

        /// <summary>Trigger Collider를 판정에 포함하는 규칙입니다.</summary>
        public QueryTriggerInteraction QueryTriggerInteraction => _queryTriggerInteraction;

        /// <summary>
        /// 이 행동이 한 번 실행될 때 검사할 히트 볼륨 개수입니다.
        /// </summary>
        public virtual int HitVolumeCount => _hitVolumes?.Length ?? 0;

        /// <summary>
        /// 지정한 순서의 히트 볼륨 데이터를 반환합니다.
        /// </summary>
        public virtual HitVolumeDefinition GetHitVolume(int index)
        {
            if (_hitVolumes == null)
                throw new IndexOutOfRangeException("히트 볼륨 배열이 비어 있습니다.");

            return _hitVolumes[index];
        }

        /// <summary>
        /// 개발자가 확인할 수 있도록 모든 서버 타격 범위를 Scene 뷰에 그립니다.
        /// </summary>
        public void DrawGizmos(Transform origin)
        {
            if (origin == null)
                return;

            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.85f);

            for (int i = 0; i < HitVolumeCount; i++)
                GetHitVolume(i).DrawGizmos(origin);

            Gizmos.color = previousColor;
        }

        protected virtual void OnValidate()
        {
            if (_hitVolumes == null)
                return;

            for (int i = 0; i < _hitVolumes.Length; i++)
            {
                HitVolumeDefinition volume = _hitVolumes[i];
                volume.Sanitize();
                _hitVolumes[i] = volume;
            }
        }
    }
}
