using UnityEngine;

namespace ProjectG.Combat
{
    /// <summary>
    /// 플레이어, 몬스터, 파괴 가능한 오브젝트가 공통으로 사용하는 서버 데미지 진입점입니다.
    /// </summary>
    public interface IDamageable
    {
        Transform DamageTransform { get; }
        int TeamId { get; }
        bool IsAlive { get; }

        /// <summary>
        /// 서버가 생성한 데미지 정보를 적용하고 체력이 실제로 변경됐는지 반환합니다.
        /// </summary>
        bool TryReceiveDamage(in DamageInfo damageInfo);
    }
}
