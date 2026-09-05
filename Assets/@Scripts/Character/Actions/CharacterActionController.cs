using System;
using System.Collections.Generic;
using ProjectG.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectG.CharacterActions
{
    /// <summary>
    /// 플레이어와 몬스터가 함께 사용하는 서버 권한 행동 실행기입니다.
    /// 플레이어는 입력 슬롯을 요청하고 서버 AI는 허용 목록의 Action SO를 직접 요청합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CharacterActionController : NetworkBehaviour
    {
        [Header("Registered Actions")]
        [FormerlySerializedAs("_actions")]
        [SerializeField] private CharacterActionDefinition[] _allowedActions =
            Array.Empty<CharacterActionDefinition>();
        [SerializeField] private PlayerActionBinding[] _playerActionBindings =
            Array.Empty<PlayerActionBinding>();

        [Header("Server Runtime")]
        [SerializeField, Range(1, 10)] private int _queueCapacity = 5;
        [SerializeField, Range(1, 64)] private int _hitBufferCapacity = 16;

        private struct QueuedAction
        {
            public CharacterActionRequest Request;
            public CharacterActionDefinition Definition;
            public double ExecuteAt;
        }

        private readonly Dictionary<PlayerActionSlot, CharacterActionDefinition> _playerDefinitions =
            new Dictionary<PlayerActionSlot, CharacterActionDefinition>();
        private readonly HashSet<CharacterActionDefinition> _allowedDefinitions =
            new HashSet<CharacterActionDefinition>();
        private readonly Dictionary<CharacterActionDefinition, double> _cooldownEnds =
            new Dictionary<CharacterActionDefinition, double>();
        private readonly List<CharacterActionDefinition> _networkDefinitions =
            new List<CharacterActionDefinition>();
        private readonly Dictionary<CharacterActionDefinition, ushort> _networkHandles =
            new Dictionary<CharacterActionDefinition, ushort>();

        private QueuedAction[] _queue;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;
        private uint _localSequence;
        private bool _isInitialized;

        private CharacterAttributes _attributes;
        private CharacterTeam _team;
        private NetworkCharacterHealth _health;
        private ServerHitboxQuery _hitboxQuery;

        /// <summary>서버가 행동을 승인해 대기열에 추가했을 때 모든 Peer에서 발생합니다.</summary>
        public event Action<CharacterActionDefinition> ActionStarted;

        /// <summary>서버가 행동의 실제 실행 시점에 도달했을 때 모든 Peer에서 발생합니다.</summary>
        public event Action<CharacterActionDefinition> ActionExecuted;

        /// <summary>
        /// 서버 또는 오프라인 환경에서 히트박스 판정이 끝났을 때 발생합니다.
        /// 두 번째 인자는 실제로 데미지가 적용된 대상 수입니다.
        /// </summary>
        public event Action<HitboxActionDefinition, int> HitboxResolved;

        /// <summary>서버 행동 대기열이 가득 찼는지 여부입니다.</summary>
        public bool IsQueueFull => _queue != null && _queueCount >= _queue.Length;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            _attributes = GetComponent<CharacterAttributes>();
            _team = GetComponent<CharacterTeam>();
            _health = GetComponent<NetworkCharacterHealth>();
            _queue = new QueuedAction[Mathf.Max(1, _queueCapacity)];
            _hitboxQuery = new ServerHitboxQuery(_hitBufferCapacity);
            BuildDefinitionLookup();
            _isInitialized = true;
        }

        private void Update()
        {
            if (!HasServerAuthority() || _queueCount == 0)
                return;

            ref QueuedAction queuedAction = ref _queue[_queueHead];
            if (GetServerTime() < queuedAction.ExecuteAt)
                return;

            CharacterActionRequest request = queuedAction.Request;
            CharacterActionDefinition definition = queuedAction.Definition;
            Dequeue();

            if (_health != null && _health.IsDepleted)
                return;

            ExecuteServer(in request, definition);
        }

        /// <summary>
        /// 소유 플레이어가 지정한 입력 슬롯의 행동을 요청합니다.
        /// 네트워크 Client는 슬롯과 조준 의도만 서버에 전송합니다.
        /// </summary>
        public bool RequestAction(
            PlayerActionSlot playerSlot,
            Vector3 aimPosition,
            Vector3 aimDirection)
        {
            EnsureInitialized();

            CharacterActionRequest request = CharacterActionRequest.Create(
                playerSlot,
                aimPosition,
                aimDirection,
                ++_localSequence);

            if (!IsSpawned)
                return TryQueuePlayerActionServer(in request);

            if (IsServer)
                return TryQueuePlayerActionServer(in request);

            if (!IsOwner || !HasPlayerAction(playerSlot) || IsQueueFull)
                return false;

            RequestActionRpc(request);
            return true;
        }

        /// <summary>
        /// 이전 CharacterActionId 요청을 PlayerActionSlot 요청으로 변환합니다.
        /// </summary>
        [Obsolete("PlayerActionSlot을 받는 RequestAction을 사용하세요.")]
        public bool RequestAction(
            CharacterActionId actionId,
            Vector3 aimPosition,
            Vector3 aimDirection)
        {
            return RequestAction((PlayerActionSlot)actionId, aimPosition, aimDirection);
        }

        /// <summary>
        /// 이전 서버 AI 요청을 플레이어 슬롯 조회 방식으로 실행합니다.
        /// 새 서버 AI는 Action SO를 직접 받는 오버로드를 사용하세요.
        /// </summary>
        public bool TryRequestActionFromServer(in CharacterActionRequest request)
        {
            return TryQueuePlayerActionServer(in request);
        }

        /// <summary>
        /// 서버 AI가 허용 목록에 등록된 Action SO를 직접 요청합니다.
        /// </summary>
        public bool TryRequestActionFromServer(
            CharacterActionDefinition definition,
            Vector3 aimPosition,
            Vector3 aimDirection,
            ulong targetNetworkObjectId = 0)
        {
            CharacterActionRequest request = CharacterActionRequest.Create(
                PlayerActionSlot.None,
                aimPosition,
                aimDirection,
                0,
                targetNetworkObjectId);

            return TryQueueActionServer(in request, definition);
        }

        /// <summary>
        /// 서버 AI가 만든 상세 요청과 허용 목록의 Action SO를 함께 실행 대기열에 추가합니다.
        /// </summary>
        public bool TryRequestActionFromServer(
            CharacterActionDefinition definition,
            in CharacterActionRequest request)
        {
            return TryQueueActionServer(in request, definition);
        }

        /// <summary>이 캐릭터가 지정한 플레이어 입력 슬롯을 사용할 수 있는지 반환합니다.</summary>
        public bool HasPlayerAction(PlayerActionSlot playerSlot)
        {
            EnsureInitialized();
            return _playerDefinitions.ContainsKey(playerSlot);
        }

        /// <summary>지정한 Action SO가 서버 AI 허용 목록에 등록되어 있는지 반환합니다.</summary>
        public bool IsDefinitionAllowed(CharacterActionDefinition definition)
        {
            EnsureInitialized();
            return definition != null && _allowedDefinitions.Contains(definition);
        }

        /// <summary>이전 코드 호환용 슬롯 조회입니다.</summary>
        [Obsolete("HasPlayerAction을 사용하세요.")]
        public bool HasDefinition(CharacterActionId actionId)
        {
            return HasPlayerAction((PlayerActionSlot)actionId);
        }

        [Rpc(SendTo.Server)]
        private void RequestActionRpc(CharacterActionRequest request)
        {
            TryQueuePlayerActionServer(in request);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyActionStartedRpc(ushort actionHandle)
        {
            if (TryResolveNetworkHandle(actionHandle, out CharacterActionDefinition definition))
                ActionStarted?.Invoke(definition);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyActionExecutedRpc(ushort actionHandle)
        {
            if (TryResolveNetworkHandle(actionHandle, out CharacterActionDefinition definition))
                ActionExecuted?.Invoke(definition);
        }

        private bool TryQueuePlayerActionServer(in CharacterActionRequest request)
        {
            EnsureInitialized();

            if (!_playerDefinitions.TryGetValue(
                    request.PlayerSlot,
                    out CharacterActionDefinition definition))
            {
                return false;
            }

            return TryQueueActionServer(in request, definition);
        }

        private bool TryQueueActionServer(
            in CharacterActionRequest request,
            CharacterActionDefinition definition)
        {
            EnsureInitialized();

            if (!HasServerAuthority() || _queueCount >= _queue.Length)
                return false;

            if (definition == null || !_allowedDefinitions.Contains(definition))
                return false;

            if (_health != null && _health.IsDepleted)
                return false;

            double now = GetServerTime();
            if (_cooldownEnds.TryGetValue(definition, out double cooldownEnd) && now < cooldownEnd)
                return false;

            _cooldownEnds[definition] = now + definition.Cooldown;
            _queue[_queueTail] = new QueuedAction
            {
                Request = request,
                Definition = definition,
                ExecuteAt = now + definition.ExecutionDelay
            };

            _queueTail = (_queueTail + 1) % _queue.Length;
            _queueCount++;
            NotifyActionStarted(definition);
            return true;
        }

        private void ExecuteServer(
            in CharacterActionRequest request,
            CharacterActionDefinition definition)
        {
            if (definition is HitboxActionDefinition hitboxDefinition)
            {
                int damagedTargetCount = _hitboxQuery.Execute(
                    transform,
                    NetworkObject,
                    _team,
                    _attributes,
                    hitboxDefinition);

                HitboxResolved?.Invoke(hitboxDefinition, damagedTargetCount);
            }

            NotifyActionExecuted(definition);
        }

        private void NotifyActionStarted(CharacterActionDefinition definition)
        {
            if (!IsSpawned)
            {
                ActionStarted?.Invoke(definition);
                return;
            }

            if (_networkHandles.TryGetValue(definition, out ushort handle))
                NotifyActionStartedRpc(handle);
        }

        private void NotifyActionExecuted(CharacterActionDefinition definition)
        {
            if (!IsSpawned)
            {
                ActionExecuted?.Invoke(definition);
                return;
            }

            if (_networkHandles.TryGetValue(definition, out ushort handle))
                NotifyActionExecutedRpc(handle);
        }

        private bool TryResolveNetworkHandle(
            ushort actionHandle,
            out CharacterActionDefinition definition)
        {
            int index = actionHandle - 1;
            if (index >= 0 && index < _networkDefinitions.Count)
            {
                definition = _networkDefinitions[index];
                return definition != null;
            }

            definition = null;
            Debug.LogWarning(
                $"[{nameof(CharacterActionController)}] Unknown action handle {actionHandle} on {name}.",
                this);
            return false;
        }

        private void Dequeue()
        {
            _queue[_queueHead] = default;
            _queueHead = (_queueHead + 1) % _queue.Length;
            _queueCount--;
        }

        private void BuildDefinitionLookup()
        {
            _playerDefinitions.Clear();
            _allowedDefinitions.Clear();
            _networkDefinitions.Clear();
            _networkHandles.Clear();

            if (_allowedActions == null)
                _allowedActions = Array.Empty<CharacterActionDefinition>();

            if (_playerActionBindings == null)
                _playerActionBindings = Array.Empty<PlayerActionBinding>();

            for (int i = 0; i < _allowedActions.Length; i++)
                RegisterAllowedDefinition(_allowedActions[i]);

            for (int i = 0; i < _playerActionBindings.Length; i++)
            {
                PlayerActionBinding binding = _playerActionBindings[i];
                CharacterActionDefinition definition = binding.Definition;
                if (binding.Slot == PlayerActionSlot.None || definition == null)
                    continue;

                RegisterAllowedDefinition(definition);
                if (_playerDefinitions.ContainsKey(binding.Slot))
                {
                    Debug.LogWarning(
                        $"[{nameof(CharacterActionController)}] Duplicate player slot {binding.Slot} on {name}.",
                        this);
                    continue;
                }

                _playerDefinitions.Add(binding.Slot, definition);
            }

            RegisterLegacyPlayerBindings();
        }

        private void RegisterAllowedDefinition(CharacterActionDefinition definition)
        {
            if (definition == null || _allowedDefinitions.Contains(definition))
                return;

            if (_networkDefinitions.Count >= ushort.MaxValue)
            {
                Debug.LogError(
                    $"[{nameof(CharacterActionController)}] Too many registered actions on {name}.",
                    this);
                return;
            }

            _allowedDefinitions.Add(definition);
            _networkDefinitions.Add(definition);
            _networkHandles.Add(definition, (ushort)_networkDefinitions.Count);
        }

        private void RegisterLegacyPlayerBindings()
        {
            for (int i = 0; i < _allowedActions.Length; i++)
            {
                CharacterActionDefinition definition = _allowedActions[i];
                if (definition == null)
                    continue;

                PlayerActionSlot slot = definition.LegacyPlayerSlot;
                if (slot == PlayerActionSlot.None || _playerDefinitions.ContainsKey(slot))
                    continue;

                _playerDefinitions.Add(slot, definition);
            }
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

        private void OnDrawGizmosSelected()
        {
            HashSet<CharacterActionDefinition> drawn = new HashSet<CharacterActionDefinition>();

            if (_allowedActions != null)
            {
                for (int i = 0; i < _allowedActions.Length; i++)
                    DrawDefinition(_allowedActions[i], drawn);
            }

            if (_playerActionBindings != null)
            {
                for (int i = 0; i < _playerActionBindings.Length; i++)
                    DrawDefinition(_playerActionBindings[i].Definition, drawn);
            }
        }

        private void DrawDefinition(
            CharacterActionDefinition definition,
            HashSet<CharacterActionDefinition> drawn)
        {
            if (definition is HitboxActionDefinition hitboxDefinition && drawn.Add(definition))
                hitboxDefinition.DrawGizmos(transform);
        }
    }
}
