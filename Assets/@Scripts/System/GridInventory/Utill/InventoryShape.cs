using System;
using UnityEngine;

/// <summary>
/// Class for storing the shape of an inventory item
/// </summary>
[Serializable]
public class InventoryShape
{
    [SerializeField] int _width;
    [SerializeField] int _height;
    [SerializeField] bool[] _shape;

    /// <summary>
    /// CTOR
    /// </summary>
    /// <param name="width">The maximum width of the shape</param>
    /// <param name="height">The maximum height of the shape</param>
    public InventoryShape(int width, int height)
    {
        _width = width;
        _height = height;
        _shape = new bool[_width * _height];
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="shape">A custom shape</param>
    public InventoryShape(bool[,] shape)
    {
        _width = shape.GetLength(0);
        _height = shape.GetLength(1);
        _shape = new bool[_width * _height];
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                _shape[GetIndex(x, y)] = shape[x, y];
            }
        }
    }

    /// <summary>
    /// Returns the width of the shapes bounding box
    /// </summary>
    public int width => _width;

    /// <summary>
    /// Returns the height of the shapes bounding box
    /// </summary>
    public int height => _height;

    /// <summary>
    /// Returns true if given local point is part of this shape
    /// </summary>
    public bool IsPartOfShape(Vector2Int localPoint)
    {
        if (localPoint.x < 0 || localPoint.x >= _width || localPoint.y < 0 || localPoint.y >= _height)
        {
            return false; // outside of shape width/height
        }

        var index = GetIndex(localPoint.x, localPoint.y);
        return _shape[index];
    }

    /*
    Converts X & Y to an index to use with _shape
    */
    private int GetIndex(int x, int y)
    {
        y = (_height - 1) - y;
        return x + _width * y;
    }

    public void Rotate()
    {
        // 1. 새 배열 생성 (크기는 동일)
        var newShape = new bool[_shape.Length];

        // 2. 가로/세로 길이 교환 준비
        var newWidth = _height;
        var newHeight = _width;

        // 3. 기존 배열을 순회하며 회전된 위치로 이동
        for (var x = 0; x < _width; x++)
        {
            for (var y = 0; y < _height; y++)
            {
                // 현재 좌표 (x, y)가 채워져 있다면
                if (IsPartOfShape(new Vector2Int(x, y)))
                {
                    // [중요] 시계방향 90도 회전 공식
                    // (x, y) -> (y, (width - 1) - x) ? -> 이건 보통 반시계
                    // (x, y) -> ((height - 1) - y, x) ? -> 이건 시계방향

                    // 현재 좌표계 기준: x는 오른쪽, y는 위쪽 증가라고 가정할 때
                    // NewX = y
                    // NewY = (OldWidth - 1) - x
                    // 하지만 배열 인덱스 GetIndex는 y축 처리가 (height-1)-y로 되어 있음.

                    // 가장 확실한 방법: 2차원 배열처럼 생각하고 변환

                    // 원본에서의 논리적 좌표 (y가 0이면 맨 아래)
                    int oldX = x;
                    int oldY = y;

                    // 시계방향 90도 회전 후 좌표
                    // 새 가로길이는 oldHeight, 새 세로길이는 oldWidth
                    // newX = oldY
                    // newY = (oldWidth - 1) - oldX;  (오른쪽으로 눕히기 때문에 X가 반전되어 Y로 감)

                    // 예: 2x1 크기 [0,0][1,0] -> 회전 -> [0,1][0,0] (1x2 크기)

                    int targetX = oldY;
                    int targetY = (_width - 1) - oldX;

                    // 변환된 좌표를 새 배열 인덱스로 변환
                    // 주의: GetIndex 로직을 그대로 풀어서 써야 함 (newHeight 기준)
                    // GetIndex 공식: x + width * ((height - 1) - y)

                    int invertedTargetY = (newHeight - 1) - targetY;
                    int newIndex = targetX + newWidth * invertedTargetY;

                    if (newIndex >= 0 && newIndex < newShape.Length)
                    {
                        newShape[newIndex] = true;
                    }
                }
            }
        }

        // 4. 데이터 갱신
        _width = newWidth;
        _height = newHeight;
        _shape = newShape;
    }

    // [추가] 깊은 복사(Deep Copy)를 위한 메서드
    // 이게 없으면 아이템을 회전할 때 원본 프리팹 데이터까지 같이 돌아가버릴 수 있습니다.
    public InventoryShape Clone()
    {
        var clone = new InventoryShape(_width, _height);
        // 배열 복사
        for (int i = 0; i < _shape.Length; i++)
        {
            clone._shape[i] = _shape[i];
        }
        return clone;
    }
}