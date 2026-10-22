using System;

namespace PdfPixel.Parsing;

/// <summary>
/// Operands of a content stream operator, in the order they were read.
/// </summary>
internal sealed class PdfOperandStack
{
    private const int InitialCapacity = 16;

    private PdfContentStreamValue[] _items = new PdfContentStreamValue[InitialCapacity];
    private int _count;

    /// <summary>
    /// Number of operands on the stack.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Pushes an operand onto the stack.
    /// </summary>
    public void Push(in PdfContentStreamValue value)
    {
        if (_count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[_count] = value;
        _count++;
    }

    /// <summary>
    /// Removes and returns the top operand.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stack is empty.</exception>
    public PdfContentStreamValue Pop()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The operand stack is empty.");
        }

        _count--;

        return _items[_count];
    }

    /// <summary>
    /// Removes the top <paramref name="count"/> operands, or every operand when fewer are on the stack,
    /// and returns them in the order they were pushed. The span is valid until the next <see cref="Push"/>.
    /// </summary>
    public ReadOnlySpan<PdfContentStreamValue> Pop(int count)
    {
        int available = Math.Min(count, _count);
        _count -= available;

        return _items.AsSpan(_count, available);
    }

    /// <summary>
    /// Removes every operand.
    /// </summary>
    public void Clear() => _count = 0;

    public ReadOnlySpan<PdfContentStreamValue> AsSpan() => _items.AsSpan(0, _count);
}
