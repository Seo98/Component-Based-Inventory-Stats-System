using System;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 서버 AI가 순서대로 실행할 행동 한 단계입니다.
    /// 구체적인 공격 종류는 enum이 아니라 Action SO 참조로 정합니다.
    /// </summary>
    [Serializable]
    public struct ServerActionPatternStep
    {
        [SerializeField] private CharacterActionDefinition _action;
        [SerializeField, Min(0f)] private float _waitBefore;
        [SerializeField, Min(0f)] private float _waitAfter;
        [SerializeField] private bool _continueWhenRejected;

        /// <summary>이 단계에서 서버에 요청할 Action SO입니다.</summary>
        public CharacterActionDefinition Action => _action;

        /// <summary>이 단계의 요청 전 대기 시간입니다.</summary>
        public float WaitBefore => Mathf.Max(0f, _waitBefore);

        /// <summary>Action의 실행 지연이 지난 뒤 추가로 기다릴 시간입니다.</summary>
        public float WaitAfter => Mathf.Max(0f, _waitAfter);

        /// <summary>쿨다운 등의 이유로 요청이 거절되어도 다음 단계로 넘어갈지 여부입니다.</summary>
        public bool ContinueWhenRejected => _continueWhenRejected;
    }

    /// <summary>
    /// 몬스터의 여러 행동과 행동 사이 간격을 데이터로 묶은 서버 전용 패턴입니다.
    /// 몬스터 종류가 늘어나도 공통 enum이나 실행기를 수정하지 않고 새 에셋만 만들 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ServerActionPattern",
        menuName = "Project G/Character Actions/Server Action Pattern")]
    public sealed class ServerActionPatternDefinition : ScriptableObject
    {
        [SerializeField] private ServerActionPatternStep[] _steps =
            Array.Empty<ServerActionPatternStep>();
        [SerializeField] private bool _loop;

        /// <summary>패턴에 등록된 단계 수입니다.</summary>
        public int StepCount => _steps != null ? _steps.Length : 0;

        /// <summary>마지막 단계 뒤 처음부터 반복할지 여부입니다.</summary>
        public bool Loop => _loop;

        /// <summary>지정한 순번의 패턴 단계를 반환합니다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">순번이 단계 범위를 벗어났습니다.</exception>
        public ServerActionPatternStep GetStep(int index)
        {
            if (_steps == null || index < 0 || index >= _steps.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _steps[index];
        }
    }
}
