using System;
using ProjectG.CharacterActions;
using Unity.Netcode;
using UnityEngine;

#pragma warning disable CS0618

namespace ProjectG.Combat
{
    /// <summary>
    /// 이전 코드와의 호환을 위한 래퍼입니다. 새 코드는 <see cref="ServerHitboxQuery"/>를 사용하세요.
    /// </summary>
    [Obsolete("새 코드는 ServerHitboxQuery를 사용하세요.")]
    public sealed class ServerMeleeHitQuery
    {
        private readonly ServerHitboxQuery _query;

        /// <summary>
        /// 이전 API와 같은 버퍼 크기로 호환 쿼리를 만듭니다.
        /// </summary>
        public ServerMeleeHitQuery(int hitBufferCapacity)
        {
            _query = new ServerHitboxQuery(hitBufferCapacity);
        }

        /// <summary>
        /// 이전 Melee Action 데이터를 범용 히트박스 쿼리로 실행합니다.
        /// </summary>
        public int Execute(
            Transform source,
            NetworkObject sourceNetworkObject,
            CharacterTeam sourceTeam,
            CharacterAttributes sourceAttributes,
            MeleeActionDefinition definition)
        {
            return _query.Execute(
                source,
                sourceNetworkObject,
                sourceTeam,
                sourceAttributes,
                definition);
        }
    }
}

#pragma warning restore CS0618
