using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryDraggedItem
{
    public enum DropMode
    {
        Added,
        Swapped,
        Returned,
        Dropped,
    }

    /// <summary>
    /// Returns the InventoryController this item originated from
    /// </summary>
    public InventoryController originalController { get; private set; }

    /// <summary>
    /// Returns the point inside the inventory from which this item originated from
    /// </summary>
    public Vector2Int originPoint { get; private set; }

    /// <summary>
    /// Returns the item-instance that is being dragged
    /// </summary>
    public IInventoryItem item { get; private set; }

    /// <summary>
    /// Gets or sets the InventoryController currently in control of this item
    /// </summary>
    public InventoryController currentController;

    private readonly Canvas _canvas;
    private readonly RectTransform _canvasRect;
    private readonly Image _image;
    private Vector2 _offset;
    private Vector2 _lastPosition;

    private float _originalRotation;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="canvas">The canvas</param>
    /// <param name="originalController">The InventoryController this item originated from</param>
    /// <param name="originPoint">The point inside the inventory from which this item originated from</param>
    /// <param name="item">The item-instance that is being dragged</param>
    /// <param name="offset">The starting offset of this item</param>
    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")]
    public InventoryDraggedItem(
        Canvas canvas,
        InventoryController originalController,
        Vector2Int originPoint,
        IInventoryItem item,
        Vector2 offset)
    {
        this.originalController = originalController;
        currentController = this.originalController;
        this.originPoint = originPoint;
        this.item = item;

        _canvas = canvas;
        _canvasRect = canvas.transform as RectTransform;

        _offset = offset;

        // Create an image representing the dragged item
        _image = new GameObject("DraggedItem").AddComponent<Image>();
        _image.raycastTarget = false;
        _image.transform.SetParent(_canvas.transform);
        _image.transform.SetAsLastSibling();
        _image.transform.localScale = Vector3.one;
        _image.sprite = item.sprite;
        _image.SetNativeSize();

        if (item is ItemInstance itemData)
        {
            _originalRotation = itemData.Rotation;
            _image.rectTransform.localRotation = Quaternion.Euler(0, 0, itemData.Rotation);
        }

        var textObj = new GameObject("CountText");
        textObj.transform.SetParent(_image.transform);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.BottomRight;
        text.fontSize = 14;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0, 0);
        rect.offsetMax = new Vector2(-4, -4);

        if (item is ItemInstance inst && inst.Data != null && inst.Data.IsStackable && inst.CurrentCount > 1)
        {
            text.text = inst.CurrentCount.ToString();
            text.enabled = true;
        }
        else
        {
            text.enabled = false;
        }
    }

    /// <summary>
    /// Gets or sets the position of this dragged item
    /// </summary>
    public Vector2 position
    {
        set
        {
            _lastPosition = value;

            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, value + _offset, camera, out var newValue);
            _image.rectTransform.localPosition = newValue;

            if (currentController != null)
            {
                item.position = currentController.ScreenToGrid(value + _offset + GetDraggedItemOffset(currentController.inventoryRenderer, item));

                bool canMerge = false;
                var targetItem = currentController.inventory.GetAtPoint(item.position);

                if (targetItem != null && item is ItemInstance sourceInst && targetItem is ItemInstance targetInst)
                {
                    if (sourceInst.Data.IsStackable && sourceInst.Data.ItemID == targetInst.Data.ItemID && !targetInst.IsFull)
                    {
                        canMerge = true;
                    }
                }
                var canAdd = currentController.inventory.CanAddAt(item, item.position) || CanSwap() || canMerge;

                currentController.inventoryRenderer.SelectItem(item, !canAdd, Color.white);
            }

            _offset = Vector2.Lerp(_offset, Vector2.zero, Time.deltaTime * 10f);
        }
    }

    /// <summary>
    /// Drop this item at the given position
    /// </summary>
    public DropMode Drop(Vector2 pos)
    {
        DropMode mode;
        if (currentController != null)
        {
            var grid = currentController.ScreenToGrid(pos + _offset + GetDraggedItemOffset(currentController.inventoryRenderer, item));
            var targetItem = currentController.inventory.GetAtPoint(grid);

            if (targetItem != null && item is ItemInstance sourceInst && targetItem is ItemInstance targetInst)
            {
                if (sourceInst.Data.IsStackable && sourceInst.Data.ItemID == targetInst.Data.ItemID && !targetInst.IsFull)
                {
                    int spaceLeft = targetInst.Data.MaxStackCount - targetInst.CurrentCount;
                    int amountToTransfer = Mathf.Min(spaceLeft, sourceInst.CurrentCount);

                    targetInst.CurrentCount += amountToTransfer;
                    sourceInst.CurrentCount -= amountToTransfer;

                    currentController.inventory.NotifyItemChanged(targetItem);

                    if (sourceInst.CurrentCount <= 0)
                    {
                        mode = DropMode.Added;
                    }
                    else
                    {
                        mode = TryRestoreToOrigin()
                            ? DropMode.Returned
                            : TryDropFromOrigin();
                    }

                    currentController.inventoryRenderer.ClearSelection();
                    Object.Destroy(_image.gameObject);
                    return mode;
                }
            }


            // Try to add new item
            if (currentController.inventory.CanAddAt(item, grid))
            {
                currentController.inventory.TryAddAt(item, grid);
                mode = DropMode.Added;
            }
            // Adding did not work, try to swap
            else if (CanSwap())
            {
                var otherItem = currentController.inventory.allItems[0];
                currentController.inventory.TryRemove(otherItem);
                originalController.inventory.TryAdd(otherItem);
                currentController.inventory.TryAdd(item);
                mode = DropMode.Swapped;
            }
            // Could not add or swap, return the item
            else
            {
                mode = TryRestoreToOrigin()
                    ? DropMode.Returned
                    : TryDropFromOrigin();
            }

            currentController.inventoryRenderer.ClearSelection();
        }
        else
        {
            mode = DropMode.Dropped;
            if (!originalController.inventory.TryForceDrop(item))
            {
                mode = TryRestoreToOrigin()
                    ? DropMode.Returned
                    : ReportRecoveryFailure();
            }
        }

        Object.Destroy(_image.gameObject);
        return mode;
    }

    /// <summary>
    /// Cancels the drag and restores the item to its source inventory.
    /// Falls back to a forced drop only when the source can no longer accept the item.
    /// </summary>
    public DropMode Cancel()
    {
        DropMode mode = DropMode.Dropped;

        if (originalController != null)
        {
            bool wasRestored = TryRestoreToOrigin();

            if (wasRestored)
            {
                mode = DropMode.Returned;
            }
            else
            {
                mode = TryDropFromOrigin();
            }

            originalController.inventoryRenderer.ClearSelection();
        }

        if (currentController != null &&
            currentController != originalController &&
            currentController.inventoryRenderer != null)
        {
            currentController.inventoryRenderer.ClearSelection();
        }

        Object.Destroy(_image.gameObject);
        return mode;
    }

    private bool TryRestoreToOrigin()
    {
        if (originalController == null)
            return false;

        RestoreRotation(item);

        if (originalController.inventory.CanAddAt(item, originPoint))
        {
            return originalController.inventory.TryAddAt(item, originPoint);
        }

        return originalController.inventory.TryAdd(item);
    }

    private DropMode TryDropFromOrigin()
    {
        if (originalController != null && originalController.inventory.TryForceDrop(item))
        {
            return DropMode.Dropped;
        }

        return ReportRecoveryFailure();
    }

    private DropMode ReportRecoveryFailure()
    {
        Debug.LogError(
            $"[Inventory] 드래그 아이템을 복구하거나 드롭할 수 없습니다: {item}",
            originalController);
        return DropMode.Returned;
    }

    /// <summary>
    /// 아이템의 회전 상태를 드래그 시작 시점(_originalRotation)으로 복구합니다.
    /// </summary>
    private void RestoreRotation(IInventoryItem targetItem)
    {
        if (targetItem is ItemInstance inst)
        {
            while (inst.Rotation != _originalRotation)
            {
                inst.Rotate();
            }
        }
    }

    /*
     * Returns the offset between dragged item and the grid 
     */
    private Vector2 GetDraggedItemOffset(InventoryRenderer renderer, IInventoryItem item)
    {
        var scale = new Vector2(
            Screen.width / _canvasRect.sizeDelta.x,
            Screen.height / _canvasRect.sizeDelta.y
        );
        var gx = -(item.width * renderer.cellSize.x / 2f) + (renderer.cellSize.x / 2);
        var gy = -(item.height * renderer.cellSize.y / 2f) + (renderer.cellSize.y / 2);
        return new Vector2(gx, gy) * scale;
    }

    /* 
     * Returns true if its possible to swap
     */
    private bool CanSwap()
    {
        if (!currentController.inventory.CanSwap(item)) return false;
        var otherItem = currentController.inventory.allItems[0];
        return originalController.inventory.CanAdd(otherItem) && currentController.inventory.CanRemove(otherItem);
    }

    public void Rotate()
    {
        // 1. 데이터 회전
        if (item is ItemInstance itemInstance)
        {
            itemInstance.Rotate();
            _image.rectTransform.localRotation = Quaternion.Euler(0, 0, itemInstance.Rotation);
        }

        position = _lastPosition;
    }
}
