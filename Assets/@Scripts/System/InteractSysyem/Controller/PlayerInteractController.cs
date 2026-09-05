using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerInteractController : NetworkBehaviour
{
    [Header("Cinemachine Settings")]
    [SerializeField] private CinemachineCamera playerVirtualCamera;

    [Header("Raycast Settings")]
    [SerializeField] private bool sendRaycast = true;
    [Tooltip("플레이어 몸체로부터의 상호작용 가능 거리")]
    [SerializeField] private float interactRange = 10.0f;

    [Tooltip("플레이어 본인은 제외하고, 상호작용 대상만 체크할 레이어 마스크")]
    [SerializeField] private LayerMask interactLayerMask = ~0; // 기본값: Everything

    private IInteractionListener _view;
    private InteractionPresenter _presenter;
    [SerializeField] private Camera mainCam;

    private PlayerInventoryController _invController;
    private IInteractable currentInteractable;
    private IRootable activeLootTarget; // 현재 열려있는 루트(파밍) 대상 (이벤트 해제용)

    private Ray debugRay;
    private bool isHitDebug;
    private Vector3 debugHitPoint;

    private bool _cursorLocked = false;
    private bool _isInteracting = false;
    private bool _interactPressed;
    private bool _cancelPressed;
    private PlayerInputRouter _inputRouter;
    public bool IsInteracting { get { return _isInteracting; } private set { _isInteracting = value; } }

    public delegate void InteractDetected(string tag, string interactionText);
    public event InteractDetected OnInteractDetected;

    public delegate void InteractUndetected();
    public event InteractUndetected OnInteractUndetected;

    public delegate void Interacted(string tag);
    public event Interacted OnInteracted;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (playerVirtualCamera != null) playerVirtualCamera.Priority = 0;
            this.enabled = false;
            return;
        }

        _invController = GetComponent<PlayerInventoryController>();
        if (_invController == null) _invController = FindAnyObjectByType<PlayerInventoryController>();

        if (playerVirtualCamera != null) playerVirtualCamera.Priority = 10;

        var uiComponent = FindAnyObjectByType<InteractionView>();

        if (uiComponent != null)
        {
            _view = uiComponent;
            _presenter = new InteractionPresenter(_view, this);
            Debug.Log($"[{name}] UI View({uiComponent.name}) 연결 성공.");
        }
        else
        {
            Debug.LogError($"[{name}] 씬에서 InteractionView를 찾을 수 없습니다.");
        }

        AssignCamera();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }

        ToggleCursor();
        BindInputRouter(GetComponent<PlayerInputRouter>());
    }

    private void AssignCamera()
    {
        mainCam = Camera.main;
        if (mainCam == null) StartCoroutine(WaitForCamera());
    }

    private IEnumerator WaitForCamera()
    {
        while (Camera.main == null) yield return null;
        mainCam = Camera.main;
    }

    public override void OnNetworkDespawn()
    {
        BindInputRouter(null);

        if (IsOwner && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (!IsOwner) return;
        AssignCamera();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (sendRaycast) CheckInteractionRaycast();

        if (_interactPressed && currentInteractable != null && !IsInteracting)
        {
            IsInteracting = true;
            _inputRouter?.SetGameplayBlocked(this, true);
            TransformInteract("Player");
            ToggleCursor();
        }

        if (_cancelPressed && IsInteracting)
        {
            CloseInteraction();
        }

        _interactPressed = false;
        _cancelPressed = false;
    }

    private void HandleInteractRequested()
    {
        _interactPressed = true;
    }

    private void HandleCancelRequested()
    {
        _cancelPressed = true;
    }

    private void HandleInventoryToggleRequested()
    {
        if (IsInteracting)
            _cancelPressed = true;
    }

    private void BindInputRouter(PlayerInputRouter router)
    {
        if (_inputRouter == router)
            return;

        if (_inputRouter != null)
        {
            _inputRouter.InteractRequested -= HandleInteractRequested;
            _inputRouter.CancelRequested -= HandleCancelRequested;
            _inputRouter.InventoryToggleRequested -= HandleInventoryToggleRequested;
            _inputRouter.SetGameplayBlocked(this, false);
        }

        _inputRouter = router;

        if (_inputRouter == null)
            return;

        _inputRouter.InteractRequested += HandleInteractRequested;
        _inputRouter.CancelRequested += HandleCancelRequested;
        _inputRouter.InventoryToggleRequested += HandleInventoryToggleRequested;
        _inputRouter.SetGameplayBlocked(this, IsInteracting);
    }

    /// <summary>
    /// [추가됨] 상호작용(루팅 등)을 안전하게 종료하고 이벤트를 해제합니다.
    /// </summary>
    private void CloseInteraction()
    {
        IsInteracting = false;
        _inputRouter?.SetGameplayBlocked(this, false);
        ToggleCursor();

        // 파밍 중이었던 대상이 있다면 이벤트 구독 해제
        if (activeLootTarget != null && _invController != null)
        {
            var lootUI = _invController.LootUI;
            if (lootUI != null && lootUI.Inventory != null)
            {
                // 드래그 아이템을 루팅 데이터에 먼저 되돌린 뒤 이벤트 연결을 끊습니다.
                InventoryController.CancelActiveDrag();
                lootUI.Inventory.onItemAdded -= activeLootTarget.AddItem;
                lootUI.Inventory.onItemRemoved -= activeLootTarget.RemoveItem;
            }

            _invController.CloseLootContainer(); // UI 닫기
            activeLootTarget = null;
        }
    }

    void ToggleCursor()
    {
        _cursorLocked = !_cursorLocked;

        Cursor.lockState = _cursorLocked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !_cursorLocked;
    }

    private void CheckInteractionRaycast()
    {
        if (mainCam == null) return;

        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        debugRay = ray;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactLayerMask))
        {
            isHitDebug = true;
            debugHitPoint = hit.point;

            if (Vector3.Distance(transform.position, hit.point) <= interactRange)
            {
                if (hit.collider.TryGetComponent(out IInteractable interactable))
                {
                    if (currentInteractable != interactable)
                    {
                        if (currentInteractable != null)
                            OnInteractUndetected?.Invoke();

                        currentInteractable = interactable;
                        OnInteractDetected?.Invoke(hit.collider.tag, interactable.GetInteractionText());
                    }
                    return;
                }
            }
        }
        else
        {
            isHitDebug = false;
            debugHitPoint = ray.GetPoint(100f);
        }

        if (currentInteractable != null)
        {
            currentInteractable = null;
            OnInteractUndetected?.Invoke();
        }
    }

    private void TransformInteract(string tag)
    {
        if (currentInteractable == null) return;

        string targetTag = currentInteractable.GetTag();

        switch (targetTag)
        {
            case "Monster":
                if (_invController != null && currentInteractable is Component comp && comp.TryGetComponent(out IRootable rootable))
                {
                    activeLootTarget = rootable;

                    var lootUI = _invController.LootUI;

                    if (lootUI != null)
                    {
                        _invController.OpenLootContainer(null);

                        lootUI.DisconnectInventory();
                        lootUI.ConnectInventory(rootable.GetConnector());
                        lootUI.AddExistingItems(rootable.GetItems());

                        lootUI.Inventory.onItemAdded -= rootable.AddItem;
                        lootUI.Inventory.onItemRemoved -= rootable.RemoveItem;

                        lootUI.Inventory.onItemAdded += rootable.AddItem;
                        lootUI.Inventory.onItemRemoved += rootable.RemoveItem;
                    }
                }
                break;

            default:
                Debug.LogWarning($"[Interact] 허용되지 않은 태그입니다. 무시함. (Tag: {targetTag})");
                return;
        }

        currentInteractable.TransformInteract(transform);

        OnInteracted?.Invoke(targetTag);
    }

    private void OnDrawGizmos()
    {
        if (mainCam == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (isHitDebug)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(debugRay.origin, debugHitPoint);
            Gizmos.DrawSphere(debugHitPoint, 0.1f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(debugRay.origin, debugRay.origin + debugRay.direction * 50f);
        }
    }
}
