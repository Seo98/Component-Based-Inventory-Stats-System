using UnityEngine;
using System;

/// <summary>
/// 인벤토리에 실제로 존재하는 개별 아이템 객체 (붕어빵)
/// </summary>
[Serializable] // 인스펙터 리스트에서 내용을 보기 위해 필수
public class ItemInstance : IInventoryItem
{
    [SerializeField] private ItemData _data;
    public ItemData Data
    {
        get => _data;
        set => _data = value; 
    }

    [SerializeField]
    private int _instanceID;

    public int InstanceID
    {
        get => _instanceID;
        private set => _instanceID = value;
    }

    [SerializeField] 
    private float _rotation = 0f;
    public float Rotation
    {
        get { return _rotation; }
        set { _rotation = value; }
    }

    public Vector2Int position { get; set; }

    private InventoryShape _currentShape;

    public ItemInstance(ItemData data, int id)
    {
        _data = data;
        InstanceID = id;

        if (data.OriginalShape != null)
        {
            _currentShape = data.OriginalShape.Clone();
        }
    }

    public string Name => _data != null ? _data.Name : "Empty";
    public Sprite sprite => _data != null ? _data.Sprite : null;
    public ItemType Type => _data != null ? _data.Type : ItemType.Utility;
    public bool canDrop => _data != null ? _data.CanDrop : true;

    public int width => _currentShape != null ? _currentShape.width : 1;
    public int height => _currentShape != null ? _currentShape.height : 1;

    private int _currentCount = 1;
    public int CurrentCount
    {
        get => _currentCount;
        set => _currentCount = _data != null ? Mathf.Clamp(value, 0, _data.MaxStackCount) : 0;
    }
    public bool IsFull => _currentCount >= Data.MaxStackCount;

    /// <summary>
    /// 현재 스택 수량을 반영한 이 인스턴스의 총 무게입니다.
    /// </summary>
    public float TotalWeight => _data != null ? _data.Weight * _currentCount : 0f;

    public bool IsPartOfShape(Vector2Int localPosition)
    {
        return _currentShape != null && _currentShape.IsPartOfShape(localPosition);
    }

    public void Rotate()
    {
        if (_currentShape == null) return;

        _currentShape.Rotate();

        _rotation -= 90f;
        if (_rotation <= -360f) _rotation += 360f;
    }
}