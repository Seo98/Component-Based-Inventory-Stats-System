using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 캐릭터가 실행하려는 행동 의도입니다.
    /// 플레이어 요청은 <see cref="PlayerSlot"/>을 사용하고 서버 AI의 직접 SO 요청에서는 None을 사용합니다.
    /// </summary>
    public struct CharacterActionRequest : INetworkSerializable
    {
        public PlayerActionSlot PlayerSlot;
        public ulong TargetNetworkObjectId;
        public Vector3 AimPosition;
        public Vector3 AimDirection;
        public uint Sequence;

        /// <summary>이전 코드 호환용 행동 ID입니다.</summary>
        [Obsolete("PlayerSlot을 사용하세요.")]
        public CharacterActionId ActionId
        {
            get => (CharacterActionId)PlayerSlot;
            set => PlayerSlot = (PlayerActionSlot)value;
        }

        /// <summary>
        /// 플레이어 슬롯과 조준 정보를 사용하는 행동 요청을 생성합니다.
        /// </summary>
        public static CharacterActionRequest Create(
            PlayerActionSlot playerSlot,
            Vector3 aimPosition,
            Vector3 aimDirection,
            uint sequence = 0,
            ulong targetNetworkObjectId = 0)
        {
            return new CharacterActionRequest
            {
                PlayerSlot = playerSlot,
                AimPosition = aimPosition,
                AimDirection = aimDirection.sqrMagnitude > 0f
                    ? aimDirection.normalized
                    : Vector3.forward,
                Sequence = sequence,
                TargetNetworkObjectId = targetNetworkObjectId
            };
        }

        /// <summary>
        /// 이전 CharacterActionId 호출을 PlayerActionSlot 요청으로 변환합니다.
        /// </summary>
        [Obsolete("PlayerActionSlot을 받는 Create를 사용하세요.")]
        public static CharacterActionRequest Create(
            CharacterActionId actionId,
            Vector3 aimPosition,
            Vector3 aimDirection,
            uint sequence = 0,
            ulong targetNetworkObjectId = 0)
        {
            return Create(
                (PlayerActionSlot)actionId,
                aimPosition,
                aimDirection,
                sequence,
                targetNetworkObjectId);
        }

        /// <summary>
        /// 행동 요청을 네트워크 버퍼에 직렬화하거나 역직렬화합니다.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            ushort rawSlot = (ushort)PlayerSlot;
            serializer.SerializeValue(ref rawSlot);

            if (serializer.IsReader)
                PlayerSlot = (PlayerActionSlot)rawSlot;

            serializer.SerializeValue(ref TargetNetworkObjectId);
            serializer.SerializeValue(ref AimPosition);
            serializer.SerializeValue(ref AimDirection);
            serializer.SerializeValue(ref Sequence);
        }
    }
}
