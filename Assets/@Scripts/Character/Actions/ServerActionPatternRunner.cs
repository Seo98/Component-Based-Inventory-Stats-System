using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 서버에서만 몬스터 행동 패턴을 순서대로 요청하는 실행기입니다.
    /// 몬스터 AI는 공격 판정 코드를 직접 가지지 않고 이 컴포넌트에 패턴과 타깃만 전달할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterActionController))]
    public sealed class ServerActionPatternRunner : NetworkBehaviour
    {
        private enum PatternPhase : byte
        {
            WaitingBefore,
            WaitingAfter
        }

        [SerializeField] private CharacterActionController _actionController;

        private ServerActionPatternDefinition _currentPattern;
        private Transform _target;
        private int _stepIndex;
        private PatternPhase _phase;
        private double _phaseEndsAt;

        /// <summary>현재 서버에서 패턴을 재생하고 있는지 여부입니다.</summary>
        public bool IsPlaying => _currentPattern != null;

        /// <summary>현재 재생 중인 패턴입니다. 재생 중이 아니면 null입니다.</summary>
        public ServerActionPatternDefinition CurrentPattern => _currentPattern;

        /// <summary>패턴 재생이 시작됐을 때 서버에서 발생합니다.</summary>
        public event Action<ServerActionPatternDefinition> PatternStarted;

        /// <summary>각 단계의 Action 요청 결과를 서버에서 전달합니다.</summary>
        public event Action<CharacterActionDefinition, bool> StepRequested;

        /// <summary>패턴이 끝났을 때 서버에서 발생합니다. 두 번째 값은 정상 완료 여부입니다.</summary>
        public event Action<ServerActionPatternDefinition, bool> PatternFinished;

        private void Awake()
        {
            if (_actionController == null)
                _actionController = GetComponent<CharacterActionController>();
        }

        private void Update()
        {
            if (_currentPattern == null)
                return;

            if (!HasServerAuthority())
            {
                FinishPattern(false);
                return;
            }

            if (GetServerTime() < _phaseEndsAt)
                return;

            if (_phase == PatternPhase.WaitingBefore)
            {
                RequestCurrentStep();
                return;
            }

            MoveToNextStep();
        }

        /// <summary>
        /// 서버에서 패턴 재생을 시작합니다. 이미 재생 중이면 restartCurrent가 true일 때만 교체합니다.
        /// </summary>
        public bool TryPlayPattern(
            ServerActionPatternDefinition pattern,
            Transform target,
            bool restartCurrent = false)
        {
            if (!HasServerAuthority() || pattern == null || pattern.StepCount == 0)
                return false;

            if (_actionController == null)
                _actionController = GetComponent<CharacterActionController>();

            if (_actionController == null)
                return false;

            if (IsPlaying)
            {
                if (!restartCurrent)
                    return false;

                FinishPattern(false);
            }

            _currentPattern = pattern;
            _target = target;
            _stepIndex = 0;
            _phase = PatternPhase.WaitingBefore;
            _phaseEndsAt = GetServerTime() + pattern.GetStep(0).WaitBefore;
            PatternStarted?.Invoke(pattern);
            return true;
        }

        /// <summary>현재 패턴을 중단합니다. 서버가 아닌 Peer의 호출은 무시합니다.</summary>
        public void CancelPattern()
        {
            if (HasServerAuthority() && IsPlaying)
                FinishPattern(false);
        }

        private void RequestCurrentStep()
        {
            ServerActionPatternStep step = _currentPattern.GetStep(_stepIndex);
            CharacterActionDefinition action = step.Action;

            Vector3 aimDirection = _target != null
                ? _target.position - transform.position
                : transform.forward;
            if (aimDirection.sqrMagnitude <= Mathf.Epsilon)
                aimDirection = transform.forward;

            Vector3 aimPosition = _target != null
                ? _target.position
                : transform.position + aimDirection.normalized;

            bool accepted = action != null && _actionController.TryRequestActionFromServer(
                action,
                aimPosition,
                aimDirection);

            StepRequested?.Invoke(action, accepted);

            if (!accepted && !step.ContinueWhenRejected)
            {
                FinishPattern(false);
                return;
            }

            _phase = PatternPhase.WaitingAfter;
            _phaseEndsAt = GetServerTime()
                + (accepted ? action.ExecutionDelay : 0f)
                + step.WaitAfter;
        }

        private void MoveToNextStep()
        {
            _stepIndex++;
            if (_stepIndex >= _currentPattern.StepCount)
            {
                if (!_currentPattern.Loop)
                {
                    FinishPattern(true);
                    return;
                }

                _stepIndex = 0;
            }

            _phase = PatternPhase.WaitingBefore;
            _phaseEndsAt = GetServerTime() + _currentPattern.GetStep(_stepIndex).WaitBefore;
        }

        private void FinishPattern(bool completed)
        {
            ServerActionPatternDefinition finishedPattern = _currentPattern;
            _currentPattern = null;
            _target = null;
            _stepIndex = 0;
            _phaseEndsAt = 0d;

            if (finishedPattern != null)
                PatternFinished?.Invoke(finishedPattern, completed);
        }

        private bool HasServerAuthority()
        {
            if (IsSpawned)
                return IsServer;

            return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_actionController == null)
                _actionController = GetComponent<CharacterActionController>();
        }
#endif
    }
}
