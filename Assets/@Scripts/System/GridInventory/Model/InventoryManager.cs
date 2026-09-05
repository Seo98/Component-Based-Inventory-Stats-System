using System;
using System.Linq;
using UnityEngine;

public class InventoryManager : IInventoryManager
{
    private Vector2Int _size = Vector2Int.one;
    private IInventoryProvider _provider;
    private Rect _fullRect;

    public InventoryManager(IInventoryProvider provider, int width, int height)
    {
        _provider = provider;
        Rebuild();
        Resize(width, height);
    }

    /// <inheritdoc />
    public int width => _size.x;

    /// <inheritdoc />
    public int height => _size.y;

    /// <inheritdoc />
    public void Resize(int newWidth, int newHeight)
    {
        _size.x = newWidth;
        _size.y = newHeight;
        RebuildRect();
    }

    private void RebuildRect()
    {
        _fullRect = new Rect(0, 0, _size.x, _size.y);
        HandleSizeChanged();
        onResized?.Invoke();
    }

    private void HandleSizeChanged()
    {
        // Drop all items that no longer fit the inventory
        for (int i = 0; i < allItems.Length;)
        {
            var item = allItems[i];
            var shouldBeDropped = false;
            var padding = Vector2.one * 0.01f;

            if (!_fullRect.Contains(item.GetMinPoint() + padding) || !_fullRect.Contains(item.GetMaxPoint() - padding))
            {
                shouldBeDropped = true;
            }

            if (shouldBeDropped)
            {
                // A successful drop rebuilds allItems, so keep the same index and
                // inspect the item that shifted into it. Failed drops must advance
                // to prevent an infinite loop while resizing the inventory.
                if (TryDrop(item))
                {
                    continue;
                }
            }

            i++;
        }
    }

    /// <inheritdoc />
    public void Rebuild()
    {
        Rebuild(false);
    }

    private void Rebuild(bool silent)
    {
        allItems = new IInventoryItem[_provider.inventoryItemCount];
        for (var i = 0; i < _provider.inventoryItemCount; i++)
        {
            allItems[i] = _provider.GetInventoryItem(i);
        }
        if (!silent) onRebuilt?.Invoke();
    }

    public void Dispose()
    {
        _provider = null;
        allItems = null;
    }

    /// <inheritdoc />
    public bool isFull
    {
        get
        {
            if (_provider.isInventoryFull) return true;

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    if (GetAtPoint(new Vector2Int(x, y)) == null) { return false; }
                }
            }
            return true;
        }
    }

    /// <inheritdoc />
    public IInventoryItem[] allItems { get; private set; }

    /// <inheritdoc />
    public event Action onRebuilt;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemDropped;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemDroppedFailed;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemAdded;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemAddedFailed;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemRemoved;

    /// <inheritdoc />
    public event Action<IInventoryItem> onItemChanged;

    /// <inheritdoc />
    public event Action onResized;

    /// <inheritdoc />
    public IInventoryItem GetAtPoint(Vector2Int point)
    {
        // Single item override
        if (_provider.inventoryRenderMode == InventoryRenderMode.Single && _provider.isInventoryFull && allItems.Length > 0)
        {
            return allItems[0];
        }

        foreach (var item in allItems)
        {
            if (item.Contains(point)) { return item; }
        }
        return null;
    }

    /// <inheritdoc />
    public IInventoryItem[] GetAtPoint(Vector2Int point, Vector2Int size)
    {
        var posibleItems = new IInventoryItem[size.x * size.y];
        var c = 0;
        for (var x = 0; x < size.x; x++)
        {
            for (var y = 0; y < size.y; y++)
            {
                posibleItems[c] = GetAtPoint(point + new Vector2Int(x, y));
                c++;
            }
        }
        return posibleItems.Distinct().Where(x => x != null).ToArray();
    }

    /// <inheritdoc />
    public bool TryRemove(IInventoryItem item)
    {
        if (!CanRemove(item)) return false;
        if (!_provider.RemoveInventoryItem(item)) return false;
        Rebuild(true);
        onItemRemoved?.Invoke(item);
        return true;
    }

    /// <inheritdoc />
    public bool TryDrop(IInventoryItem item)
    {
        if (!CanDrop(item) || !_provider.DropInventoryItem(item))
        {
            onItemDroppedFailed?.Invoke(item);
            return false;
        }
        Rebuild(true);
        onItemDropped?.Invoke(item);
        return true;
    }

    internal bool TryForceDrop(IInventoryItem item)
    {
        if (!item.canDrop)
        {
            onItemDroppedFailed?.Invoke(item);
            return false;
        }
        onItemDropped?.Invoke(item);
        return true;
    }

    /// <inheritdoc />
    public bool CanAddAt(IInventoryItem item, Vector2Int point)
    {
        if (!_provider.CanAddInventoryItem(item) || _provider.isInventoryFull)
        {
            return false;
        }

        if (_provider.inventoryRenderMode == InventoryRenderMode.Single)
        {
            return true;
        }

        var previousPoint = item.position;
        item.position = point;
        try
        {
            var padding = Vector2.one * 0.01f;

            // Check if item is outside of inventory
            if (!_fullRect.Contains(item.GetMinPoint() + padding) ||
                !_fullRect.Contains(item.GetMaxPoint() - padding))
            {
                return false;
            }

            // Check if item overlaps another item already in the inventory
            foreach (var otherItem in allItems)
            {
                if (item.Overlaps(otherItem))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            // CanAddAt is a query. It must never leave the inspected item mutated.
            item.position = previousPoint;
        }

    }

    /// <inheritdoc />
    public bool TryAddAt(IInventoryItem item, Vector2Int point)
    {
        if (!CanAddAt(item, point))
        {
            onItemAddedFailed?.Invoke(item);
            return false;
        }

        var previousPoint = item.position;
        switch (_provider.inventoryRenderMode)
        {
            case InventoryRenderMode.Single:
                item.position = GetCenterPosition(item);
                break;
            case InventoryRenderMode.Grid:
                item.position = point;
                break;
            default:
                throw new NotImplementedException($"InventoryRenderMode.{_provider.inventoryRenderMode.ToString()} have not yet been implemented");
        }

        if (!_provider.AddInventoryItem(item))
        {
            item.position = previousPoint;
            onItemAddedFailed?.Invoke(item);
            return false;
        }

        Rebuild(true);
        onItemAdded?.Invoke(item);
        return true;
    }

    /// <inheritdoc />
    public void NotifyItemChanged(IInventoryItem item)
    {
        if (item == null || !Contains(item))
            return;

        onItemChanged?.Invoke(item);
    }

    /// <inheritdoc />
    public bool CanAdd(IInventoryItem item)
    {
        if (item is ItemInstance incoming && incoming.Data != null && incoming.Data.IsStackable)
        {
            int totalSpaceAvailable = 0;

            foreach (var existingItem in allItems)
            {
                if (existingItem is ItemInstance target && target.Data.ItemID == incoming.Data.ItemID)
                {
                    totalSpaceAvailable += (target.Data.MaxStackCount - target.CurrentCount);
                }
            }

            if (totalSpaceAvailable >= incoming.CurrentCount)
            {
                return true;
            }
        }

        Vector2Int point;
        if (!Contains(item) && GetFirstPointThatFitsItem(item, out point))
        {
            return CanAddAt(item, point);
        }
        return false;
    }

    /// <inheritdoc />
    public bool TryAdd(IInventoryItem item)
    {
        if (item is ItemInstance incoming && incoming.Data != null && incoming.Data.IsStackable)
        {
            foreach (var existingItem in allItems)
            {
                if (existingItem is ItemInstance target &&
                    target.Data.ItemID == incoming.Data.ItemID &&
                    !target.IsFull)
                {
                    int spaceLeft = target.Data.MaxStackCount - target.CurrentCount;
                    int amountToTransfer = Mathf.Min(spaceLeft, incoming.CurrentCount);

                    target.CurrentCount += amountToTransfer;
                    incoming.CurrentCount -= amountToTransfer;
                    NotifyItemChanged(target);

                    if (incoming.CurrentCount <= 0)
                    {
                        return true;
                    }
                }
            }
        }

        // 병합하고도 아이템 개수가 남았거나(새 칸 필요), 애초에 스택이 안 되는 장비 아이템일 경우
        if (!CanAdd(item)) return false;
        Vector2Int point;
        return GetFirstPointThatFitsItem(item, out point) && TryAddAt(item, point);
    }

    /// <inheritdoc />
    public bool CanSwap(IInventoryItem item)
    {
        return _provider.inventoryRenderMode == InventoryRenderMode.Single &&
            DoesItemFit(item) &&
            _provider.CanAddInventoryItem(item);
    }

    /// <inheritdoc />
    public void DropAll()
    {
        var itemsToDrop = allItems.ToArray();
        foreach (var item in itemsToDrop)
        {
            TryDrop(item);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var item in allItems)
        {
            TryRemove(item);
        }
    }

    /// <inheritdoc />
    public bool Contains(IInventoryItem item) => allItems.Contains(item);


    /// <inheritdoc />
    public bool CanRemove(IInventoryItem item) => Contains(item) && _provider.CanRemoveInventoryItem(item);

    /// <inheritdoc />
    public bool CanDrop(IInventoryItem item) => Contains(item) && _provider.CanDropInventoryItem(item) && item.canDrop;

    /*
     * Get first free point that will fit the given item
     */
    private bool GetFirstPointThatFitsItem(IInventoryItem item, out Vector2Int point)
    {
        if (DoesItemFit(item))
        {
            for (var x = 0; x < width - (item.width - 1); x++)
            {
                for (var y = 0; y < height - (item.height - 1); y++)
                {
                    point = new Vector2Int(x, y);
                    if (CanAddAt(item, point)) return true;
                }
            }
        }
        point = Vector2Int.zero;
        return false;
    }

    /* 
     * Returns true if given items physically fits within this inventory
     */
    private bool DoesItemFit(IInventoryItem item) => item.width <= width && item.height <= height;

    /*
     * Returns the center post position for a given item within this inventory
     */
    private Vector2Int GetCenterPosition(IInventoryItem item)
    {
        return new Vector2Int(
            (_size.x - item.width) / 2,
            (_size.y - item.height) / 2
        );
    }
}
