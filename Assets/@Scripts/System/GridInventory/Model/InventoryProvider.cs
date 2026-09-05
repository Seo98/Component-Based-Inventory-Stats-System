using System;
using System.Collections.Generic;

public class InventoryProvider : IInventoryProvider
{
    private List<IInventoryItem> _items = new List<IInventoryItem>();
    private int _maximumAlowedItemCount;
    ItemType _allowedItem;

    /// <summary>
    /// CTOR
    /// </summary>
    public InventoryProvider(InventoryRenderMode renderMode, int maximumAlowedItemCount = -1, ItemType allowedItem = ItemType.Any)
    {
        inventoryRenderMode = renderMode;
        _maximumAlowedItemCount = maximumAlowedItemCount;
        _allowedItem = allowedItem;
    }

    public int inventoryItemCount => _items.Count;

    public InventoryRenderMode inventoryRenderMode { get; private set; }

    public bool isInventoryFull
    {
        get
        {
            if (_maximumAlowedItemCount < 0) return false;
            return inventoryItemCount >= _maximumAlowedItemCount;
        }
    }

    public bool AddInventoryItem(IInventoryItem item)
    {
        if (!_items.Contains(item))
        {
            _items.Add(item);
            return true;
        }
        return false;
    }

    public bool DropInventoryItem(IInventoryItem item)
    {
        return RemoveInventoryItem(item);
    }

    public IInventoryItem GetInventoryItem(int index)
    {
        return _items[index];
    }

    public bool CanAddInventoryItem(IInventoryItem item)
    {
        if (item == null) return false;

        if (_allowedItem == ItemType.Any) return true;

        if (item is ItemInstance instance)
        {
            return instance.Type == _allowedItem;
        }
        else if (item is ItemData data)
        {
            return data.Type == _allowedItem;
        }
        return false;
    }

    public bool CanRemoveInventoryItem(IInventoryItem item)
    {
        return true;
    }

    public bool CanDropInventoryItem(IInventoryItem item)
    {
        return true;
    }

    public bool RemoveInventoryItem(IInventoryItem item)
    {
        return _items.Remove(item);
    }
}