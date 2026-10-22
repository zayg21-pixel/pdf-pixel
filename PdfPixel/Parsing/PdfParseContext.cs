using PdfPixel.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// High-performance PDF parse context over one or many memory chunks treated uniformly.
/// </summary>
internal ref struct PdfParseContext
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    /// <summary>
    /// Construct context from a single memory block (wrapped as one chunk).
    /// </summary>
    /// <param name="memory">The memory block to parse.</param>
    public PdfParseContext(in ReadOnlyMemory<byte> memory) => _data = memory.Span;

    /// <summary>
    /// Construct a content stream context from a single memory block.
    /// </summary>
    /// <param name="memory">The memory block to parse.</param>
    /// <param name="document">Document the content stream belongs to, which its values are parsed for.</param>
    internal PdfParseContext(in ReadOnlyMemory<byte> memory, IPdfDocumentInternal document)
    {
        _data = memory.Span;
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Cursor = new PdfContentStreamCursor();
    }

    /// <summary>
    /// Construct a content stream context over <paramref name="data"/>, such as the extent of a single value of another context.
    /// </summary>
    /// <param name="data">The data to parse.</param>
    /// <param name="document">Document the content stream belongs to, which its values are parsed for.</param>
    internal PdfParseContext(in ReadOnlySpan<byte> data, IPdfDocumentInternal document)
    {
        _data = data;
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Construct a content stream context from a list of memory chunks.
    /// </summary>
    /// <param name="chunks">The list of chunks representing one logical stream.</param>
    /// <param name="document">Document the content stream belongs to, which its values are parsed for.</param>
    internal PdfParseContext(List<ReadOnlyMemory<byte>> chunks, IPdfDocumentInternal document)
    {
        if (chunks == null)
        {
            throw new ArgumentNullException(nameof(chunks));
        }

        if (chunks.Count == 1)
        {
            _data = chunks[0].Span;
        }
        else
        {
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

        Document = document ?? throw new ArgumentNullException(nameof(document));
        Cursor = new PdfContentStreamCursor();
    }

    /// <summary>
    /// Document of the content stream the context reads, which its names, strings, arrays, dictionaries and inline images
    /// are parsed for; <see langword="null"/> for a context that does not read a content stream.
    /// </summary>
    internal IPdfDocumentInternal? Document { get; }

    /// <summary>
    /// Positions shared by every copy of the context of a whole content stream; <see langword="null"/> for any other context.
    /// </summary>
    internal PdfContentStreamCursor? Cursor { get; }

    /// <summary>
    /// Current absolute position in the logical stream.
    /// </summary>
    public int Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _position;
        }

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
        get
        {
            return _data.Length;
        }
    }

    /// <summary>
    /// True if positioned at or beyond end of stream.
    /// </summary>
    public bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _position >= _data.Length;
        }
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

    /// <summary>
    /// Get the bytes of the logical stream in the given range.
    /// </summary>
    /// <param name="start">Absolute position of the first byte.</param>
    /// <param name="length">Number of bytes.</param>
    /// <returns>The bytes in the range.</returns>
    internal readonly ReadOnlySpan<byte> Slice(int start, int length) => _data.Slice(start, length);

    /// <inheritdoc/>
    public override string ToString() => PdfEncodingExtensions.PdfDefault.GetString(_data);
}
