using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 행동의 변경되지 않는 설정 데이터입니다.
    /// 실행 중 상태는 <see cref="CharacterActionController"/>가 관리합니다.
    /// </summary>
    public abstract class CharacterActionDefinition : ScriptableObject
    {
        [FormerlySerializedAs("_actionId")]
        [SerializeField, HideInInspector] private PlayerActionSlot _legacyPlayerSlot;
        [SerializeField, Min(0f)] private float _cooldown = 0.5f;
        [SerializeField, Min(0f)] private float _executionDelay = 0.2f;

        /// <summary>
        /// 이전 Action 에셋을 Player Action Binding으로 자동 연결할 때만 사용하는 마이그레이션 값입니다.
        /// 새 에셋의 행동 슬롯은 <see cref="PlayerActionBinding"/>에서 설정합니다.
        /// </summary>
        public PlayerActionSlot LegacyPlayerSlot => _legacyPlayerSlot;

        /// <summary>이 행동이 다시 실행될 수 있을 때까지의 시간입니다.</summary>
        public float Cooldown => _cooldown;

        /// <summary>행동이 승인된 뒤 실제 효과가 실행될 때까지의 시간입니다.</summary>
        public float ExecutionDelay => _executionDelay;

        /// <summary>이전 코드가 읽을 수 있도록 유지한 행동 ID입니다.</summary>
        [Obsolete("Action SO에서 ID를 읽지 말고 PlayerActionBinding 또는 직접 SO 참조를 사용하세요.")]
        public CharacterActionId ActionId => (CharacterActionId)_legacyPlayerSlot;
    }
}
