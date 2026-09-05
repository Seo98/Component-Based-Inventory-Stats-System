using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 서버가 생성한 한 번의 데미지 시도 정보입니다.
    /// </summary>
    public readonly struct DamageInfo
    {
        public DamageInfo(
            float rawDamage,
            float minimumDamage,
            ulong sourceNetworkObjectId,
            int sourceTeamId,
            Vector3 hitPoint,
            Vector3 hitDirection)
        {
            RawDamage = Mathf.Max(0f, rawDamage);
            MinimumDamage = Mathf.Max(0f, minimumDamage);
            SourceNetworkObjectId = sourceNetworkObjectId;
            SourceTeamId = sourceTeamId;
            HitPoint = hitPoint;
            HitDirection = hitDirection.sqrMagnitude > 0f
                ? hitDirection.normalized
                : Vector3.forward;
        }

        public float RawDamage { get; }
        public float MinimumDamage { get; }
        public ulong SourceNetworkObjectId { get; }
        public int SourceTeamId { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitDirection { get; }
    }
}
