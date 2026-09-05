using UnityEngine;

public interface IInventoryItem
{
    ItemData Data { get; }
    
    int InstanceID { get; }
    
    string Name { get; }
    
    float Rotation { get; }

    /// <summary>
    /// The sprite of this item
    /// </summary>
    Sprite sprite { get; }

    /// <summary>
    /// Returns this items position within an inventory
    /// </summary>
    Vector2Int position { get; set; }

    /// <summary>
    /// The width of this item
    /// </summary>
    int width { get; }

    /// <summary>
    /// The height of this item
    /// </summary>
    int height { get; }

    /// <summary>
    /// Returns true if given local position is part 
    /// of this items shape
    /// </summary>
    bool IsPartOfShape(Vector2Int localPosition);

    /// <summary>
    /// Returns true if this item can be dropped on the ground
    /// </summary>
    bool canDrop { get; }
}