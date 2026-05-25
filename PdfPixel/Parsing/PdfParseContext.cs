using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// High-performance PDF parse context over one or many memory chunks treated uniformly.
/// </summary>
public ref struct PdfParseContext
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>
    /// Construct context from a single memory block (wrapped as one chunk).
    /// </summary>
    /// <param name="memory">The memory block to parse.</param>
    public PdfParseContext(in ReadOnlyMemory<byte> memory) => _data = memory.Span;

    /// <summary>
    /// Construct context from a list of memory chunks.
    /// </summary>
    /// <param name="chunks">The list of chunks representing one logical stream.</param>
    public PdfParseContext(List<ReadOnlyMemory<byte>> chunks)
    {
        if (chunks == null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        Memory<byte> total = new byte[chunks.Sum(x => x.Length + 1) - 1];
        int start = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            ReadOnlyMemory<byte> chunk = chunks[i];
            chunk.CopyTo(total.Slice(start, chunk.Length));
            start += chunk.Length + 1; // add 1 padding byte between chunks to set correct /0 separator between chunks
        }

        _data = total.Span;
    }

    /// <summary>
    /// Current absolute position in the logical stream.
    /// </summary>
    public int Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _position; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value < 0)
            {
                value = 0;
            }

            if (value > _data.Length)
            {
                value = _data.Length;
            }

            _position = value;
        }
    }

    /// <summary>
    /// Total length in bytes across all chunks.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _data.Length; }
    }

    /// <summary>
    /// True if positioned at or beyond end of stream.
    /// </summary>
    public bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _position >= _data.Length; }
    }

    /// <summary>
    /// Peek a byte at offset from current position without advancing. Returns 0 if out of range.
    /// Fast path: if target lies within cached chunk boundaries, index directly.
    /// Slow path: resolve chunk by setting current chunk based on target position.
    /// </summary>
    /// <param name="offset">Optional offset from current position.</param>
    /// <returns>The byte value or 0 if out of range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte PeekByte(int offset = 0)
    {
        int target = _position + offset;
        if (target < 0 || target >= _data.Length)
        {
            return 0;
        }

        return _data[_position + offset];
    }

    /// <summary>
    /// Read a byte and advance position. Returns 0 at end.
    /// </summary>
    /// <returns>The byte value or 0 if at end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        byte value = PeekByte();
        Advance(1);
        return value;
    }

    /// <summary>
    /// Advance by count bytes (clamped). Negative count allowed (rewind).
    /// </summary>
    /// <param name="count">Number of bytes to advance (can be negative).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        int newPosition = _position + count;
        if (newPosition < 0)
        {
            newPosition = 0;
        }

        if (newPosition > _data.Length)
        {
            newPosition = _data.Length;
        }

        _position = newPosition;
    }

    public override string ToString() => Text.EncodingExtensions.PdfDefault.GetString(_data);
}
