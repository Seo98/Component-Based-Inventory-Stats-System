using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 데이터(InventoryManager)를 기반으로 실제 UI를 화면에 그려주는 렌더러 클래스입니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InventoryRenderer : MonoBehaviour
{
    [SerializeField, Tooltip("인벤토리를 구성하는 각 셀(칸)의 픽셀 크기입니다.")]
    private Vector2Int _cellSize = new Vector2Int(32, 32);

    [SerializeField, Tooltip("비어있는 셀에 사용할 스프라이트 이미지")]
    private Sprite _cellSpriteEmpty = null;

    [SerializeField, Tooltip("선택된 셀(아이템을 놓을 수 있음)에 사용할 스프라이트 (보통 초록색)")]
    private Sprite _cellSpriteSelected = null;

    [SerializeField, Tooltip("차단된 셀(아이템을 놓을 수 없음)에 사용할 스프라이트 (보통 빨간색)")]
    private Sprite _cellSpriteBlocked = null;

    /// <summary>
    /// 렌더러가 참조하고 있는 실제 인벤토리 로직 매니저
    /// </summary>
    internal IInventoryManager inventory;

    /// <summary>
    /// 인벤토리 렌더링 모드 (단일 슬롯, 그리드 등)
    /// </summary>
    InventoryRenderMode _renderMode;

    private bool _haveListeners;

    // UI 이미지 객체를 재사용하기 위한 오브젝트 풀
    private Pool<Image> _gridPool; // 배경 격자용 (순수 이미지)
    private Pool<InventoryItemUI> _itemPool; // 아이템용 (이미지 + 텍스트)

    // 배경 그리드 이미지 배열
    private Image[] _grids;

    // 아이템 데이터와 실제 UI 이미지를 연결하는 딕셔너리
    private Dictionary<IInventoryItem, InventoryItemUI> _items = new Dictionary<IInventoryItem, InventoryItemUI>();

    /// <summary>
    /// 컴포넌트가 활성화될 때 UI 풀(Pool)을 생성하고 초기화합니다.
    /// 배경 격자용 풀과 아이템 아이콘용 풀을 분리하여 메모리를 최적화합니다.
    /// </summary>
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        var imageContainer = new GameObject("UI Pool").AddComponent<RectTransform>();
        imageContainer.SetParent(transform);
        imageContainer.localPosition = Vector3.zero;
        imageContainer.localScale = Vector3.one;

        // [1] 배경 그리드용 풀
        _gridPool = new Pool<Image>(delegate
        {
            var image = new GameObject("GridImage").AddComponent<Image>();
            image.transform.SetParent(imageContainer);
            image.transform.localScale = Vector3.one;
            return image;
        });

        // [2] 아이템 전용 풀 (이미지 + TextMeshPro + InventoryItemUI 컴포넌트)
        _itemPool = new Pool<InventoryItemUI>(delegate
        {
            var itemObj = new GameObject("ItemUI");
            itemObj.transform.SetParent(imageContainer);
            itemObj.transform.localScale = Vector3.one;

            var iconImage = itemObj.AddComponent<Image>();
            var itemUI = itemObj.AddComponent<InventoryItemUI>();

            var textObj = new GameObject("CountText");
            textObj.transform.SetParent(itemObj.transform);
            var text = textObj.AddComponent<TextMeshProUGUI>();

            text.alignment = TextAlignmentOptions.BottomRight;
            text.fontSize = 14;
            text.color = Color.white;
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(-4, -4);

            itemUI.iconImage = iconImage;
            itemUI.countText = text;

            return itemUI;
        });
    }

    /// <summary>
    /// 렌더링할 인벤토리 데이터를 연결하고 렌더링 모드를 설정합니다.
    /// </summary>
    /// <param name="inventoryManager">연결할 실제 인벤토리 데이터 매니저</param>
    /// <param name="renderMode">인벤토리를 그릴 방식 (그리드, 단일 슬롯 등)</param>
    public void SetInventory(IInventoryManager inventoryManager, InventoryRenderMode renderMode)
    {
        OnDisable();
        inventory = inventoryManager ?? throw new ArgumentNullException(nameof(inventoryManager));
        _renderMode = renderMode;
        OnEnable();
    }

    /// <summary>
    /// 이 렌더러가 부착된 UI의 RectTransform 컴포넌트입니다.
    /// </summary>
    public RectTransform rectTransform { get; private set; }

    /// <summary>
    /// 인벤토리를 구성하는 1칸(Cell)의 픽셀 크기입니다.
    /// </summary>
    public Vector2 cellSize => _cellSize;

    /// <summary>
    /// 컴포넌트 활성화 시 인벤토리 데이터의 이벤트를 구독하고 화면을 처음으로 그립니다.
    /// </summary>
    void OnEnable()
    {
        if (inventory != null && !_haveListeners)
        {
            if (_cellSpriteEmpty == null) throw new NullReferenceException("Sprite for empty cell is null");
            if (_cellSpriteSelected == null) throw new NullReferenceException("Sprite for selected cells is null.");
            if (_cellSpriteBlocked == null) throw new NullReferenceException("Sprite for blocked cells is null.");

            inventory.onRebuilt += ReRenderAllItems;
            inventory.onItemAdded += HandleItemAdded;
            inventory.onItemRemoved += HandleItemRemoved;
            inventory.onItemDropped += HandleItemRemoved;
            inventory.onItemChanged += RefreshItemUI;
            inventory.onResized += HandleResized;
            _haveListeners = true;

            ReRenderGrid();
            ReRenderAllItems();
        }
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 메모리 누수를 막기 위해 이벤트 구독을 해제합니다.
    /// </summary>
    void OnDisable()
    {
        if (inventory != null && _haveListeners)
        {
            inventory.onRebuilt -= ReRenderAllItems;
            inventory.onItemAdded -= HandleItemAdded;
            inventory.onItemRemoved -= HandleItemRemoved;
            inventory.onItemDropped -= HandleItemRemoved;
            inventory.onItemChanged -= RefreshItemUI;
            inventory.onResized -= HandleResized;
            _haveListeners = false;
        }
    }

    /// <summary>
    /// 기존 배경 격자 이미지를 모두 지우고, 현재 인벤토리 크기에 맞춰 다시 생성합니다.
    /// </summary>
    private void ReRenderGrid()
    {
        if (_grids != null)
        {
            for (var i = 0; i < _grids.Length; i++)
            {
                _grids[i].gameObject.SetActive(false);
                RecycleGridImage(_grids[i]);
                _grids[i].transform.SetSiblingIndex(i);
            }
        }
        _grids = null;

        var containerSize = new Vector2(cellSize.x * inventory.width, cellSize.y * inventory.height);
        Image grid;
        switch (_renderMode)
        {
            case InventoryRenderMode.Single:
                grid = CreateGridImage(_cellSpriteEmpty, true);
                grid.rectTransform.SetAsFirstSibling();
                grid.type = Image.Type.Sliced;
                grid.rectTransform.localPosition = Vector3.zero;
                grid.rectTransform.sizeDelta = containerSize;
                _grids = new[] { grid };
                break;

            default:
                var topLeft = new Vector3(-containerSize.x / 2, -containerSize.y / 2, 0);
                var halfCellSize = new Vector3(cellSize.x / 2, cellSize.y / 2, 0);
                _grids = new Image[inventory.width * inventory.height];
                var c = 0;

                for (int y = 0; y < inventory.height; y++)
                {
                    for (int x = 0; x < inventory.width; x++)
                    {
                        grid = CreateGridImage(_cellSpriteEmpty, true);
                        grid.gameObject.name = "Grid " + c;
                        grid.rectTransform.SetAsFirstSibling();
                        grid.type = Image.Type.Sliced;
                        grid.rectTransform.localPosition = topLeft + new Vector3(cellSize.x * ((inventory.width - 1) - x), cellSize.y * y, 0) + halfCellSize;
                        grid.rectTransform.sizeDelta = cellSize;
                        _grids[c] = grid;
                        c++;
                    }
                }
                break;
        }

        rectTransform.sizeDelta = containerSize;
    }

    /// <summary>
    /// 현재 표시중인 모든 아이템 UI를 풀에 반납하고, 인벤토리 데이터를 바탕으로 처음부터 다시 그립니다.
    /// </summary>
    private void ReRenderAllItems()
    {
        foreach (var itemUI in _items.Values)
        {
            itemUI.Clear();
            itemUI.gameObject.SetActive(false);
            _itemPool.Recycle(itemUI);
        }
        _items.Clear();

        foreach (var item in inventory.allItems)
        {
            HandleItemAdded(item);
        }
    }

    /// <summary>
    /// 인벤토리에 아이템이 추가되었을 때 호출되어 UI를 생성하고 올바른 위치에 배치합니다.
    /// </summary>
    /// <param name="item">추가된 아이템 데이터 객체</param>
    private void HandleItemAdded(IInventoryItem item)
    {
        var itemUI = _itemPool.Take();
        itemUI.gameObject.SetActive(true);
        itemUI.transform.SetAsLastSibling();

        var imgRect = itemUI.iconImage.rectTransform;
        imgRect.sizeDelta = new Vector2(item.sprite.rect.width, item.sprite.rect.height);
        itemUI.iconImage.raycastTarget = false;

        if (_renderMode == InventoryRenderMode.Single)
            imgRect.localPosition = rectTransform.rect.center;
        else
            imgRect.localPosition = GetItemOffset(item);

        _items.Add(item, itemUI);
        RefreshItemUI(item);
    }

    /// <summary>
    /// 아이템의 개수 텍스트, 중첩 가능 여부, 회전 상태 등의 시각적 정보를 갱신합니다.
    /// </summary>
    /// <param name="item">갱신할 아이템 데이터</param>
    public void RefreshItemUI(IInventoryItem item)
    {
        if (item == null) return;

        if (_items.TryGetValue(item, out InventoryItemUI itemUI))
        {
            if (item is ItemInstance itemInst)
            {
                bool isStackable = itemInst.Data != null && itemInst.Data.IsStackable;
                itemUI.Setup(item.sprite, itemInst.CurrentCount, isStackable, itemInst.Rotation);
            }
            else
            {
                itemUI.Setup(item.sprite, 1, false, 0f);
            }
        }
    }

    /// <summary>
    /// 인벤토리에서 아이템이 제거되었을 때 호출되어 해당 UI 객체를 오브젝트 풀로 반납합니다.
    /// </summary>
    /// <param name="item">제거된 아이템 데이터</param>
    private void HandleItemRemoved(IInventoryItem item)
    {
        if (_items.ContainsKey(item))
        {
            var itemUI = _items[item];
            itemUI.Clear();
            itemUI.gameObject.SetActive(false);
            itemUI.gameObject.name = "ItemUI";
            _itemPool.Recycle(itemUI);
            _items.Remove(item);
        }
    }

    /// <summary>
    /// 인벤토리의 가로/세로 칸 수가 변경되었을 때 화면을 전체적으로 다시 갱신합니다.
    /// </summary>
    private void HandleResized()
    {
        ReRenderGrid();
        ReRenderAllItems();
    }

    /// <summary>
    /// 오브젝트 풀에서 빈 배경 격자용 이미지를 하나 꺼내어 반환합니다.
    /// </summary>
    /// <param name="sprite">격자에 적용할 스프라이트 (빈 칸)</param>
    /// <param name="raycastTarget">레이캐스트 타겟 허용 여부 (클릭 감지용)</param>
    /// <returns>생성된 배경 격자 Image 컴포넌트</returns>
    private Image CreateGridImage(Sprite sprite, bool raycastTarget)
    {
        var img = _gridPool.Take();
        img.gameObject.SetActive(true);
        img.sprite = sprite;
        img.rectTransform.sizeDelta = new Vector2(img.sprite.rect.width, img.sprite.rect.height);
        img.transform.SetAsFirstSibling();
        img.type = Image.Type.Sliced;
        img.raycastTarget = raycastTarget;
        return img;
    }

    /// <summary>
    /// 사용이 끝난 배경 격자용 이미지를 오브젝트 풀에 반납합니다.
    /// </summary>
    /// <param name="image">반납할 Image 컴포넌트</param>
    private void RecycleGridImage(Image image)
    {
        image.gameObject.name = "GridImage";
        image.gameObject.SetActive(false);
        _gridPool.Recycle(image);
    }

    /// <summary>
    /// 특정 아이템이 드래그 중일 때, 아이템이 차지할 공간(셀)을 선택(하이라이트) 상태로 표시합니다.
    /// </summary>
    /// <param name="item">대상 아이템의 형태와 위치 정보</param>
    /// <param name="blocked">아이템을 놓을 수 없는 상태인지 여부 (True일 경우 붉은색 표시)</param>
    /// <param name="color">셀에 적용할 덧씌우기(Tint) 색상</param>
    public void SelectItem(IInventoryItem item, bool blocked, Color color)
    {
        if (item == null) { return; }
        ClearSelection();

        switch (_renderMode)
        {
            case InventoryRenderMode.Single:
                _grids[0].sprite = blocked ? _cellSpriteBlocked : _cellSpriteSelected;
                _grids[0].color = color;
                break;
            default:
                for (var x = 0; x < item.width; x++)
                {
                    for (var y = 0; y < item.height; y++)
                    {
                        if (item.IsPartOfShape(new Vector2Int(x, y)))
                        {
                            var p = item.position + new Vector2Int(x, y);
                            if (p.x >= 0 && p.x < inventory.width && p.y >= 0 && p.y < inventory.height)
                            {
                                var index = p.y * inventory.width + ((inventory.width - 1) - p.x);
                                _grids[index].sprite = blocked ? _cellSpriteBlocked : _cellSpriteSelected;
                                _grids[index].color = color;
                            }
                        }
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 하이라이트된 모든 배경 셀의 상태를 원래(빈 셀) 상태로 복구합니다.
    /// </summary>
    public void ClearSelection()
    {
        if (_grids == null)
            return;

        for (var i = 0; i < _grids.Length; i++)
        {
            _grids[i].sprite = _cellSpriteEmpty;
            _grids[i].color = Color.white;
        }
    }

    /// <summary>
    /// 아이템의 논리적 2D 인덱스 좌표를 UI에 표시될 실제 픽셀 로컬 좌표로 변환합니다.
    /// </summary>
    /// <param name="item">변환할 아이템 객체</param>
    /// <returns>UI 캔버스 상의 로컬 위치 오프셋</returns>
    internal Vector2 GetItemOffset(IInventoryItem item)
    {
        var x = (-(inventory.width * 0.5f) + item.position.x + item.width * 0.5f) * cellSize.x;
        var y = (-(inventory.height * 0.5f) + item.position.y + item.height * 0.5f) * cellSize.y;
        return new Vector2(x, y);
    }
}
