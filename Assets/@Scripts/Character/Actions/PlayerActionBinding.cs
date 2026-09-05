using System;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 플레이어 입력 슬롯 하나를 이 캐릭터가 실행할 실제 Action SO에 연결합니다.
    /// </summary>
    [Serializable]
    public struct PlayerActionBinding
    {
        [SerializeField] private PlayerActionSlot _slot;
        [SerializeField] private CharacterActionDefinition _definition;

        /// <summary>플레이어가 요청할 입력 슬롯입니다.</summary>
        public PlayerActionSlot Slot => _slot;

        /// <summary>해당 슬롯으로 실행할 Action SO입니다.</summary>
        public CharacterActionDefinition Definition => _definition;

        /// <summary>
        /// 슬롯과 Action SO를 연결한 값을 만듭니다.
        /// </summary>
        public PlayerActionBinding(PlayerActionSlot slot, CharacterActionDefinition definition)
        {
            _slot = slot;
            _definition = definition;
        }
    }
}
