using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리의 사용자 입력(클릭, 드래그, 드롭 등)을 처리하는 컨트롤러 클래스입니다.
/// Unity의 UI 이벤트 인터페이스들을 구현하여 작동합니다.
/// </summary>
public class InventoryController : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerExitHandler, IPointerEnterHandler,
        IInventoryController
{
    // 드래그 중인 아이템은 static으로 선언되어 모든 인벤토리 컨트롤러가 공유합니다.
    // 이를 통해 서로 다른 인벤토리 간(예: 루팅 -> 플레이어)에 아이템 이동이 가능해집니다.
    private static InventoryDraggedItem _draggedItem;

    /// <inheritdoc />
    /// <summary>마우스가 아이템 위에 올라갔을 때 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemHovered;

    /// <inheritdoc />
    /// <summary>아이템을 집어 들었을 때(드래그 시작) 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemPickedUp;

    /// <inheritdoc />
    /// <summary>아이템이 인벤토리에 정상적으로 추가되었을 때 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemAdded;

    /// <inheritdoc />
    /// <summary>아이템 위치가 서로 교체되었을 때 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemSwapped;

    /// <inheritdoc />
    /// <summary>놓을 곳이 없어 원래 위치로 되돌아갔을 때 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemReturned;

    /// <inheritdoc />
    /// <summary>인벤토리 밖(땅)으로 아이템이 버려졌을 때 발생하는 이벤트</summary>
    public event Action<IInventoryItem> onItemDropped;

    private Canvas _canvas;
    internal InventoryRenderer inventoryRenderer;

    /// <summary>
    /// 렌더러와 연결된 실제 인벤토리 데이터 매니저
    /// </summary>
    internal InventoryManager inventory => (InventoryManager)inventoryRenderer.inventory;

    private IInventoryItem _itemToDrag;
    private PointerEventData _currentEventData;
    private IInventoryItem _lastHoveredItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _draggedItem = null;
    }

    /// <summary>
    /// 초기화: 렌더러와 캔버스를 찾아서 설정합니다.
    /// </summary>
    void Awake()
    {
        inventoryRenderer = GetComponent<InventoryRenderer>();
        if (inventoryRenderer == null) { throw new NullReferenceException("Renderer를 찾을 수 없습니다. InventoryController는 InventoryRenderer와 함께 있어야 합니다."); }

        // 부모 객체들 중 가장 최상단(또는 가장 가까운) 캔버스를 찾습니다.
        var canvases = GetComponentsInParent<Canvas>();
        if (canvases.Length == 0) { throw new NullReferenceException("Canvas를 찾을 수 없습니다."); }
        _canvas = canvases[canvases.Length - 1];
    }

    /// <summary>
    /// 그리드를 클릭했을 때 호출됩니다. (IPointerDownHandler)
    /// 드래그할 아이템을 미리 식별합니다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_draggedItem != null) return;

        // 클릭한 화면 좌표를 그리드 좌표로 변환하여 해당 위치의 아이템을 가져옵니다.
        var grid = ScreenToGrid(eventData.position);
        _itemToDrag = inventory.GetAtPoint(grid);
    }

    /// <summary>
    /// 드래그가 시작될 때 호출됩니다. (IBeginDragHandler)
    /// Shift + 드래그 시 아이템을 절반으로 분할합니다.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        inventoryRenderer.ClearSelection();

        if (_itemToDrag == null || _draggedItem != null) return;

        bool isSplitting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        IInventoryItem itemBeingDragged = _itemToDrag; // 기본적으로는 통째로 듦

        // 스택 가능하고, 2개 이상일 때만 분할 가능
        if (isSplitting && _itemToDrag is ItemInstance itemInst && itemInst.Data.IsStackable && itemInst.CurrentCount > 1)
        {
            int splitAmount = Mathf.FloorToInt(itemInst.CurrentCount / 2f);

            itemInst.CurrentCount -= splitAmount;

            ItemInstance splitClone = itemInst.Data.CreateInstance();
            splitClone.CurrentCount = splitAmount;

            splitClone.position = itemInst.position;

            itemBeingDragged = splitClone;

            // [TODO] UI 숫자 갱신 (원본 아이템의 숫자가 줄어들었으므로 렌더러 새로고침 필요)
            inventory.NotifyItemChanged(_itemToDrag);
        }
        else
        {
            inventory.TryRemove(_itemToDrag);
        }


        // 드래그 시 아이템 이미지가 마우스 위치에 정확히 오도록 오프셋 계산
        var localPosition = ScreenToLocalPositionInRenderer(eventData.position);
        var itemOffest = inventoryRenderer.GetItemOffset(itemBeingDragged); // 교체됨
        var offset = itemOffest - localPosition;

        // 드래그 전용 객체 생성
        _draggedItem = new InventoryDraggedItem(
            _canvas,
            this,
            _itemToDrag.position, // 분할 실패 시 돌아갈 원래 좌표
            itemBeingDragged,     // 교체됨
            offset
        );

        onItemPickedUp?.Invoke(itemBeingDragged); 
    }

    /// <summary>
    /// 드래그 중일 때 매 프레임 호출됩니다. (IDragHandler)
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        // 현재 이벤트 데이터를 저장해두고 Update()에서 처리합니다.
        _currentEventData = eventData;
        if (_draggedItem != null)
        {
            // _draggedItem.Position = eventData.position; // (Update문에서 처리하므로 주석 처리됨)
        }
    }

    /// <summary>
    /// 드래그가 끝났을 때(마우스를 놓았을 때) 호출됩니다. (IEndDragHandler)
    /// 아이템을 놓거나, 교체하거나, 버리는 로직을 수행합니다.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_draggedItem == null) return;

        InventoryDraggedItem completedDrag = _draggedItem;
        _draggedItem = null;
        try
        {
            InventoryDraggedItem.DropMode mode = completedDrag.Drop(eventData.position);
            NotifyDragCompleted(completedDrag.item, mode);
        }
        finally
        {
            ResetPointerState();
        }
    }

    /// <summary>
    /// Cancels the globally active inventory drag, if any, and restores its item safely.
    /// </summary>
    /// <returns>True when an active drag was cancelled.</returns>
    public static bool CancelActiveDrag()
    {
        InventoryDraggedItem activeDrag = _draggedItem;
        if (activeDrag == null)
            return false;

        _draggedItem = null;
        InventoryController originalController = activeDrag.originalController;
        InventoryDraggedItem.DropMode mode = activeDrag.Cancel();

        if (originalController != null)
        {
            originalController.NotifyDragCompleted(activeDrag.item, mode);
            originalController.ResetPointerState();
        }

        return true;
    }

    private void OnDisable()
    {
        InventoryDraggedItem activeDrag = _draggedItem;
        bool ownsActiveDrag =
            activeDrag != null &&
            (activeDrag.originalController == this || activeDrag.currentController == this);

        if (ownsActiveDrag)
        {
            CancelActiveDrag();
        }

        ClearHoveredItem();
        ResetPointerState();
    }

    /// <summary>
    /// 마우스 포인터가 이 인벤토리 영역을 벗어났을 때 호출됩니다. (IPointerExitHandler)
    /// 드래그 중이라면 '현재 컨트롤러 없음(허공)' 상태로 만듭니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_draggedItem != null)
        {
            // 아이템이 현재 컨트롤러(인벤토리) 영역을 벗어났음을 표시
            _draggedItem.currentController = null;
            inventoryRenderer.ClearSelection();
        }
        else { ClearHoveredItem(); }
        _currentEventData = null;
    }

    /// <summary>
    /// 마우스 포인터가 이 인벤토리 영역에 들어왔을 때 호출됩니다. (IPointerEnterHandler)
    /// 드래그 중인 아이템의 제어권을 이 컨트롤러로 가져옵니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_draggedItem != null)
        {
            // 드래그 중인 아이템이 내 구역으로 들어왔으므로 제어권을 가짐
            _draggedItem.currentController = this;
        }
        _currentEventData = eventData;
    }

    /// <summary>
    /// 매 프레임 실행되는 로직입니다.
    /// 호버링 감지 및 드래그 중인 아이템의 위치 업데이트, 회전 입력을 처리합니다.
    /// </summary>
    void Update()
    {
        if (_currentEventData == null) return;

        if (_draggedItem == null)
        {
            // 드래그 중이 아닐 때는 마우스 아래에 있는 아이템을 감지(Hover)합니다.
            var grid = ScreenToGrid(_currentEventData.position);
            var item = inventory.GetAtPoint(grid);
            if (item == _lastHoveredItem) return;
            onItemHovered?.Invoke(item);
            _lastHoveredItem = item;
        }
        else
        {
            // 드래그 중일 때는 아이템 위치를 마우스 위치로 갱신합니다.
            _draggedItem.position = _currentEventData.position;

            // [회전 로직]
            // 현재 마우스가 위치한 인벤토리(currentController)가 '나(this)'일 때만 회전 입력을 처리합니다.
            // 이 조건이 없으면 겹쳐있는 여러 컨트롤러나, 드래그를 시작한 컨트롤러가 동시에 입력을 받아
            // 아이템이 2번 회전(180도)하는 버그가 발생합니다.
            if (_draggedItem.currentController == this)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    _draggedItem.Rotate();
                }
            }
        }
    }

    /// <summary>
    /// 현재 호버링 중인 아이템 정보를 초기화합니다.
    /// </summary>
    private void ClearHoveredItem()
    {
        if (_lastHoveredItem != null)
        {
            onItemHovered?.Invoke(null);
        }
        _lastHoveredItem = null;
    }

    private void NotifyDragCompleted(
        IInventoryItem draggedItem,
        InventoryDraggedItem.DropMode mode)
    {
        switch (mode)
        {
            case InventoryDraggedItem.DropMode.Added:
                onItemAdded?.Invoke(draggedItem);
                break;
            case InventoryDraggedItem.DropMode.Swapped:
                onItemSwapped?.Invoke(draggedItem);
                break;
            case InventoryDraggedItem.DropMode.Returned:
                onItemReturned?.Invoke(draggedItem);
                break;
            case InventoryDraggedItem.DropMode.Dropped:
                onItemDropped?.Invoke(draggedItem);
                ClearHoveredItem();
                break;
        }
    }

    private void ResetPointerState()
    {
        _itemToDrag = null;
        _currentEventData = null;
    }

    /// <summary>
    /// 화면상(Screen)의 좌표를 인벤토리 그리드(Grid) 좌표(x, y)로 변환합니다.
    /// </summary>
    internal Vector2Int ScreenToGrid(Vector2 screenPoint)
    {
        var pos = ScreenToLocalPositionInRenderer(screenPoint);
        var sizeDelta = inventoryRenderer.rectTransform.sizeDelta;
        pos.x += sizeDelta.x / 2;
        pos.y += sizeDelta.y / 2;
        return new Vector2Int(Mathf.FloorToInt(pos.x / inventoryRenderer.cellSize.x), Mathf.FloorToInt(pos.y / inventoryRenderer.cellSize.y));
    }

    /// <summary>
    /// 화면 좌표를 렌더러(RectTransform) 기준의 로컬 좌표로 변환합니다.
    /// </summary>
    private Vector2 ScreenToLocalPositionInRenderer(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            inventoryRenderer.rectTransform,
            screenPosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out var localPosition
        );
        return localPosition;
    }
}
