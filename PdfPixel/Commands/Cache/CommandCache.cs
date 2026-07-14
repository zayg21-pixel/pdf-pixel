using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// General-purpose cache for entries produced during command execution, keyed by
/// <see cref="ICommandCacheKey"/>. Owns stored entries and disposes them when replaced or cleared.
/// </summary>
public sealed class CommandCache : IDisposable
{
    private readonly Dictionary<ICommandCacheKey, ICommandCacheItem> _entries = [];

    /// <summary>
    /// Returns the entry stored under <paramref name="key"/>, or null when nothing is stored for it.
    /// </summary>
    public ICommandCacheItem? GetEntry(ICommandCacheKey key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        return (_entries.TryGetValue(key, out ICommandCacheItem? entry)) ? entry : null;
    }

    /// <summary>
    /// Stores <paramref name="item"/> under <paramref name="key"/>, disposing any entry previously
    /// stored under the same key.
    /// </summary>
    public void StoreEntry(ICommandCacheKey key, ICommandCacheItem item)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (_entries.TryGetValue(key, out ICommandCacheItem? existing))
        {
            existing.Dispose();
        }

        _entries[key] = item;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ICommandCacheItem entry in _entries.Values)
        {
            entry.Dispose();
        }

        _entries.Clear();
    }
}
