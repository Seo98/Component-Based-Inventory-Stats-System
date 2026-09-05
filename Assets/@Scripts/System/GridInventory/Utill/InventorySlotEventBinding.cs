using System;

/// <summary>
/// Owns one inventory subscription group and releases it deterministically.
/// </summary>
public sealed class InventorySlotEventBinding : IDisposable
{
    private readonly IInventoryManager _inventory;
    private readonly Action<IInventoryItem> _onAdded;
    private readonly Action<IInventoryItem> _onRemoved;
    private readonly Action<IInventoryItem> _onDropped;
    private bool _disposed;

    /// <summary>
    /// Subscribes the supplied callbacks to an inventory.
    /// </summary>
    public InventorySlotEventBinding(
        IInventoryManager inventory,
        Action<IInventoryItem> onAdded = null,
        Action<IInventoryItem> onRemoved = null,
        Action<IInventoryItem> onDropped = null)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _onAdded = onAdded;
        _onRemoved = onRemoved;
        _onDropped = onDropped;

        if (_onAdded != null) _inventory.onItemAdded += _onAdded;
        if (_onRemoved != null) _inventory.onItemRemoved += _onRemoved;
        if (_onDropped != null) _inventory.onItemDropped += _onDropped;
    }

    /// <summary>
    /// Unsubscribes every callback owned by this binding.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_onAdded != null) _inventory.onItemAdded -= _onAdded;
        if (_onRemoved != null) _inventory.onItemRemoved -= _onRemoved;
        if (_onDropped != null) _inventory.onItemDropped -= _onDropped;

        _disposed = true;
    }
}
