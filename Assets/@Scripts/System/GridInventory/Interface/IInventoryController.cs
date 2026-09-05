using UnityEngine;
using System;

public interface IInventoryController
{
    event Action<IInventoryItem> onItemHovered;
    event Action<IInventoryItem> onItemPickedUp;
    event Action<IInventoryItem> onItemAdded;
    event Action<IInventoryItem> onItemSwapped;
    event Action<IInventoryItem> onItemReturned;
    event Action<IInventoryItem> onItemDropped;
}
