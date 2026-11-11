using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Objects;

/// <summary>
/// Values kept in one array that doubles as they are added, read as a span. Clearing keeps the array, so a list
/// reused across uses stops allocating once it has grown.
/// </summary>
/// <typeparam name="T">The type of the values.</typeparam>
public sealed class PdfValueList<T>
{
    private const int MinimumCapacity = 16;

    private T[] _items = [];

    /// <summary>
    /// Gets the number of values.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets the values, in the order they were added. Valid until the list is next changed.
    /// </summary>
    public ReadOnlySpan<T> Items => _items.AsSpan(0, Count);

    /// <summary>
    /// Adds <paramref name="item"/> after the values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in T item)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, Math.Max(MinimumCapacity, _items.Length * 2));
        }

        _items[Count] = item;
        Count++;
    }

    /// <summary>
    /// Removes the last <paramref name="count"/> values, or every value when fewer are held, and returns them in the
    /// order they were added. The span is valid until the next <see cref="Add"/>.
    /// </summary>
    public ReadOnlySpan<T> RemoveLast(int count)
    {
        int removed = Math.Min(count, Count);
        Count -= removed;

        return _items.AsSpan(Count, removed);
    }

    /// <summary>
    /// Removes every value, keeping the array.
    /// </summary>
    public void Clear() => Count = 0;
}
