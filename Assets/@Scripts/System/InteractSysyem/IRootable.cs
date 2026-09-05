using System.Collections.Generic;
using UnityEngine;

public interface IRootable
{
    void AddItem(IInventoryItem item);
    void RemoveItem(IInventoryItem item);
    void DropItem(IInventoryItem item);
    List<IInventoryItem> GetConnector();
    List<IInventoryItem> GetItems();
}
