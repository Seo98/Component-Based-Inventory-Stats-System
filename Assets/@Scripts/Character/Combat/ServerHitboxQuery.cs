using System.Collections.Generic;
using ProjectG.CharacterActions;
using Unity.Netcode;
using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 할당 없는 물리 쿼리로 여러 히트 볼륨을 검사하고 서버 권한 데미지를 적용합니다.
    /// 한 행동의 여러 볼륨이나 여러 Collider가 같은 대상을 찾아도 한 번만 처리합니다.
    /// </summary>
    public sealed class ServerHitboxQuery
    {
        private readonly Collider[] _hitBuffer;
        private readonly HashSet<int> _processedTargets;

        /// <summary>
        /// 동시에 검사할 수 있는 Collider 수를 지정해 쿼리 서비스를 만듭니다.
        /// </summary>
        public ServerHitboxQuery(int hitBufferCapacity)
        {
            int capacity = Mathf.Max(1, hitBufferCapacity);
            _hitBuffer = new Collider[capacity];
            _processedTargets = new HashSet<int>(capacity);
        }

        /// <summary>
        /// 행동의 모든 히트 볼륨을 검사하고 실제로 데미지를 받은 대상 수를 반환합니다.
        /// </summary>
        public int Execute(
            Transform source,
            NetworkObject sourceNetworkObject,
            CharacterTeam sourceTeam,
            CharacterAttributes sourceAttributes,
            HitboxActionDefinition definition)
        {
            if (source == null || definition == null)
                return 0;

            _processedTargets.Clear();

            int damagedTargetCount = 0;
            int sourceTeamId = sourceTeam != null ? sourceTeam.TeamId : 0;
            float attackPower =
                sourceAttributes != null && sourceAttributes.AttackPower != null
                    ? Mathf.Max(0f, sourceAttributes.AttackPower.Value)
                    : 0f;

            for (int volumeIndex = 0; volumeIndex < definition.HitVolumeCount; volumeIndex++)
            {
                HitVolumeDefinition volume = definition.GetHitVolume(volumeIndex);
                volume.GetWorldPose(source, out Vector3 center, out Quaternion rotation);
                int hitCount = QueryVolume(source, in volume, center, rotation, definition);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    Collider hit = _hitBuffer[hitIndex];
                    _hitBuffer[hitIndex] = null;

                    if (TryDamageTarget(
                            hit,
                            center,
                            source,
                            sourceNetworkObject,
                            sourceTeamId,
                            attackPower,
                            definition))
                    {
                        damagedTargetCount++;
                    }
                }
            }

            return damagedTargetCount;
        }

        private int QueryVolume(
            Transform source,
            in HitVolumeDefinition volume,
            Vector3 center,
            Quaternion rotation,
            HitboxActionDefinition definition)
        {
            switch (volume.Shape)
            {
                case HitVolumeShape.Box:
                    return Physics.OverlapBoxNonAlloc(
                        center,
                        volume.GetWorldBoxHalfExtents(source),
                        _hitBuffer,
                        rotation,
                        definition.TargetLayers,
                        definition.QueryTriggerInteraction);

                case HitVolumeShape.Capsule:
                    volume.GetWorldCapsule(
                        source,
                        out Vector3 pointA,
                        out Vector3 pointB,
                        out float radius);
                    return Physics.OverlapCapsuleNonAlloc(
                        pointA,
                        pointB,
                        radius,
                        _hitBuffer,
                        definition.TargetLayers,
                        definition.QueryTriggerInteraction);

                default:
                    return Physics.OverlapSphereNonAlloc(
                        center,
                        volume.GetWorldSphereRadius(source),
                        _hitBuffer,
                        definition.TargetLayers,
                        definition.QueryTriggerInteraction);
            }
        }

        private bool TryDamageTarget(
            Collider hit,
            Vector3 queryCenter,
            Transform source,
            NetworkObject sourceNetworkObject,
            int sourceTeamId,
            float attackPower,
            HitboxActionDefinition definition)
        {
            if (hit == null)
                return false;

            IDamageable damageable = FindDamageableInParents(hit.transform);
            if (damageable == null || !damageable.IsAlive)
                return false;

            Transform damageTransform = damageable.DamageTransform;
            if (damageTransform == null || damageTransform == source || damageTransform.IsChildOf(source))
                return false;

            int targetInstanceId = damageTransform.GetInstanceID();
            if (!_processedTargets.Add(targetInstanceId))
                return false;

            if (!definition.AllowFriendlyFire &&
                sourceTeamId != 0 &&
                damageable.TeamId == sourceTeamId)
            {
                return false;
            }

            Vector3 hitDirection = damageTransform.position - source.position;
            DamageInfo damageInfo = new DamageInfo(
                attackPower * definition.DamageMultiplier,
                definition.MinimumDamage,
                sourceNetworkObject != null ? sourceNetworkObject.NetworkObjectId : 0,
                sourceTeamId,
                hit.ClosestPoint(queryCenter),
                hitDirection);

            return damageable.TryReceiveDamage(in damageInfo);
        }

        private static IDamageable FindDamageableInParents(Transform current)
        {
            while (current != null)
            {
                IDamageable damageable = current.GetComponent<IDamageable>();
                if (damageable != null)
                    return damageable;

                current = current.parent;
            }

            return null;
        }
    }
}
