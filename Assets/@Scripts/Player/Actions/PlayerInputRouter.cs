using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 소유자의 장치 입력을 읽어 의미 있는 명령으로 플레이어 시스템에 전달합니다.
/// 데미지, 스태미나 같은 게임 규칙은 명령을 받는 각 기능 컴포넌트가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerCameraLook))]
[RequireComponent(typeof(PlayerJump))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerRoll))]
[RequireComponent(typeof(PlayerItemUse))]
public sealed class PlayerInputRouter : NetworkBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField, Min(0f)] private float _mouseLookScale = 0.05f;
    [SerializeField, Min(0f)] private float _gamepadLookDegreesPerSecond = 120f;

    private static PlayerInputRouter _local;
    private readonly HashSet<int> _gameplayBlockers = new HashSet<int>();

    private PlayerMotor _motor;
    private PlayerCameraLook _cameraLook;
    private PlayerJump _jump;
    private PlayerCombat _combat;
    private PlayerRoll _roll;
    private PlayerItemUse _itemUse;

    private InputActionAsset _runtimeActions;
    private InputActionMap _playerMap;
    private InputActionMap _uiMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _attackAction;
    private InputAction _rollAction;
    private InputAction _potionAction;
    private InputAction _interactAction;
    private InputAction _inventoryAction;
    private InputAction _cancelAction;
    private InputAction _hotbar1Action;
    private InputAction _hotbar2Action;
    private InputAction _hotbar3Action;
    private InputAction _hotbar4Action;
    private InputAction _hotbar5Action;
    private InputAction _unequipAction;
    private bool _isLocalInputActive;

    /// <summary>
    /// 로컬 플레이어 입력 라우터가 생성되거나 사라질 때 발생합니다.
    /// 씬 UI 시스템이 자신의 로컬 입력 공급자를 찾는 용도로만 사용합니다.
    /// </summary>
    public static event Action<PlayerInputRouter> LocalRouterChanged;

    public event Action InteractRequested;
    public event Action InventoryToggleRequested;
    public event Action CancelRequested;
    public event Action<int> HotbarRequested;
    public event Action UnequipRequested;

    public static PlayerInputRouter Local => _local;
    public bool IsGameplayInputBlocked => _gameplayBlockers.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _local = null;
        LocalRouterChanged = null;
    }

    private void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        _cameraLook = GetComponent<PlayerCameraLook>();
        _jump = GetComponent<PlayerJump>();
        _combat = GetComponent<PlayerCombat>();
        _roll = GetComponent<PlayerRoll>();
        _itemUse = GetComponent<PlayerItemUse>();
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            ActivateLocalInput();
    }

    private void OnDisable()
    {
        DeactivateLocalInput();
    }

    public override void OnDestroy()
    {
        if (_runtimeActions != null)
            Destroy(_runtimeActions);

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            ActivateLocalInput();
        else
            DeactivateLocalInput();
    }

    public override void OnNetworkDespawn()
    {
        DeactivateLocalInput();
    }

    private void Update()
    {
        if (!_isLocalInputActive)
            return;

        if (IsGameplayInputBlocked)
        {
            _motor.SetMoveInput(Vector2.zero);
            return;
        }

        _motor.SetMoveInput(_moveAction.ReadValue<Vector2>());

        Vector2 look = _lookAction.ReadValue<Vector2>();
        if (_lookAction.activeControl != null && _lookAction.activeControl.device is Pointer)
            look *= _mouseLookScale;
        else
            look *= _gamepadLookDegreesPerSecond * Time.unscaledDeltaTime;

        _cameraLook.ApplyLookInput(look);
    }

    /// <summary>
    /// UI 또는 상호작용 컴포넌트가 소유한 게임 입력 차단 상태를 추가하거나 제거합니다.
    /// 인벤토리와 취소 같은 전역 명령은 차단 중에도 사용할 수 있습니다.
    /// </summary>
    public void SetGameplayBlocked(UnityEngine.Object owner, bool blocked)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (blocked)
            _gameplayBlockers.Add(ownerId);
        else
            _gameplayBlockers.Remove(ownerId);

        if (IsGameplayInputBlocked)
            _motor.SetMoveInput(Vector2.zero);
    }

    /// <summary>
    /// 자신이 아닌 다른 시스템이 현재 게임 입력을 차단 중인지 반환합니다.
    /// </summary>
    public bool IsGameplayBlockedByOtherThan(UnityEngine.Object owner)
    {
        if (_gameplayBlockers.Count == 0)
            return false;

        if (owner == null)
            return true;

        int ownId = owner.GetInstanceID();
        return _gameplayBlockers.Count > (_gameplayBlockers.Contains(ownId) ? 1 : 0);
    }

    private void ActivateLocalInput()
    {
        if (_isLocalInputActive)
            return;

        if (!TryCreateRuntimeActions())
        {
            enabled = false;
            return;
        }

        SubscribeActionCallbacks();
        _playerMap.Enable();
        _uiMap.Enable();
        _isLocalInputActive = true;
        SetLocalRouter(this);
    }

    private void DeactivateLocalInput()
    {
        if (!_isLocalInputActive)
            return;

        _motor.SetMoveInput(Vector2.zero);
        _playerMap.Disable();
        _uiMap.Disable();
        UnsubscribeActionCallbacks();
        _gameplayBlockers.Clear();
        _isLocalInputActive = false;

        if (_local == this)
            SetLocalRouter(null);
    }

    private bool TryCreateRuntimeActions()
    {
        if (_runtimeActions != null)
            return true;

        if (_inputActions == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerInputRouter)}] InputActionAsset is not assigned on {name}.",
                this);
            return false;
        }

        _runtimeActions = Instantiate(_inputActions);
        _playerMap = _runtimeActions.FindActionMap("Player", false);
        _uiMap = _runtimeActions.FindActionMap("UI", false);

        if (_playerMap == null || _uiMap == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerInputRouter)}] Player or UI action map is missing.",
                this);
            return false;
        }

        _moveAction = FindRequiredAction(_playerMap, "Move");
        _lookAction = FindRequiredAction(_playerMap, "Look");
        _jumpAction = FindRequiredAction(_playerMap, "Jump");
        _attackAction = FindRequiredAction(_playerMap, "Attack");
        _rollAction = FindRequiredAction(_playerMap, "Roll");
        _potionAction = FindRequiredAction(_playerMap, "UsePotion");
        _interactAction = FindRequiredAction(_playerMap, "Interact");
        _inventoryAction = FindRequiredAction(_playerMap, "Inventory");
        _hotbar1Action = FindRequiredAction(_playerMap, "Hotbar1");
        _hotbar2Action = FindRequiredAction(_playerMap, "Hotbar2");
        _hotbar3Action = FindRequiredAction(_playerMap, "Hotbar3");
        _hotbar4Action = FindRequiredAction(_playerMap, "Hotbar4");
        _hotbar5Action = FindRequiredAction(_playerMap, "Hotbar5");
        _unequipAction = FindRequiredAction(_playerMap, "Unequip");
        _cancelAction = FindRequiredAction(_uiMap, "Cancel");

        return _moveAction != null &&
               _lookAction != null &&
               _jumpAction != null &&
               _attackAction != null &&
               _rollAction != null &&
               _potionAction != null &&
               _interactAction != null &&
               _inventoryAction != null &&
               _hotbar1Action != null &&
               _hotbar2Action != null &&
               _hotbar3Action != null &&
               _hotbar4Action != null &&
               _hotbar5Action != null &&
               _unequipAction != null &&
               _cancelAction != null;
    }

    private InputAction FindRequiredAction(InputActionMap map, string actionName)
    {
        InputAction action = map.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogError(
                $"[{nameof(PlayerInputRouter)}] Required action '{map.name}/{actionName}' is missing.",
                this);
        }

        return action;
    }

    private void SubscribeActionCallbacks()
    {
        _jumpAction.performed += HandleJump;
        _attackAction.performed += HandleAttack;
        _rollAction.performed += HandleRoll;
        _potionAction.performed += HandlePotion;
        _interactAction.performed += HandleInteract;
        _inventoryAction.performed += HandleInventory;
        _cancelAction.performed += HandleCancel;
        _hotbar1Action.performed += HandleHotbar1;
        _hotbar2Action.performed += HandleHotbar2;
        _hotbar3Action.performed += HandleHotbar3;
        _hotbar4Action.performed += HandleHotbar4;
        _hotbar5Action.performed += HandleHotbar5;
        _unequipAction.performed += HandleUnequip;
    }

    private void UnsubscribeActionCallbacks()
    {
        _jumpAction.performed -= HandleJump;
        _attackAction.performed -= HandleAttack;
        _rollAction.performed -= HandleRoll;
        _potionAction.performed -= HandlePotion;
        _interactAction.performed -= HandleInteract;
        _inventoryAction.performed -= HandleInventory;
        _cancelAction.performed -= HandleCancel;
        _hotbar1Action.performed -= HandleHotbar1;
        _hotbar2Action.performed -= HandleHotbar2;
        _hotbar3Action.performed -= HandleHotbar3;
        _hotbar4Action.performed -= HandleHotbar4;
        _hotbar5Action.performed -= HandleHotbar5;
        _unequipAction.performed -= HandleUnequip;
    }

    private bool CanRouteGameplay()
    {
        return _isLocalInputActive && !IsGameplayInputBlocked;
    }

    private void HandleJump(InputAction.CallbackContext _) { if (CanRouteGameplay()) _jump.RequestJump(); }
    private void HandleAttack(InputAction.CallbackContext _) { if (CanRouteGameplay()) _combat.RequestPrimaryAttack(); }
    private void HandleRoll(InputAction.CallbackContext _) { if (CanRouteGameplay()) _roll.RequestRoll(); }
    private void HandlePotion(InputAction.CallbackContext _) { if (CanRouteGameplay()) _itemUse.RequestPotionUse(); }
    private void HandleInteract(InputAction.CallbackContext _) { if (CanRouteGameplay()) InteractRequested?.Invoke(); }
    private void HandleInventory(InputAction.CallbackContext _) { InventoryToggleRequested?.Invoke(); }
    private void HandleCancel(InputAction.CallbackContext _) { CancelRequested?.Invoke(); }
    private void HandleHotbar1(InputAction.CallbackContext _) { if (CanRouteGameplay()) HotbarRequested?.Invoke(1); }
    private void HandleHotbar2(InputAction.CallbackContext _) { if (CanRouteGameplay()) HotbarRequested?.Invoke(2); }
    private void HandleHotbar3(InputAction.CallbackContext _) { if (CanRouteGameplay()) HotbarRequested?.Invoke(3); }
    private void HandleHotbar4(InputAction.CallbackContext _) { if (CanRouteGameplay()) HotbarRequested?.Invoke(4); }
    private void HandleHotbar5(InputAction.CallbackContext _) { if (CanRouteGameplay()) HotbarRequested?.Invoke(5); }
    private void HandleUnequip(InputAction.CallbackContext _) { if (CanRouteGameplay()) UnequipRequested?.Invoke(); }

    private static void SetLocalRouter(PlayerInputRouter router)
    {
        if (_local == router)
            return;

        _local = router;
        LocalRouterChanged?.Invoke(_local);
    }
}
