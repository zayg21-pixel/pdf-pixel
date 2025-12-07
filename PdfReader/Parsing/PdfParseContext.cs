using PdfReader.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfReader.Parsing;

/// <summary>
/// High-performance PDF parse context over one or many memory chunks treated uniformly.
/// Maintains a cached current chunk and performs lazy binary search when crossing chunk boundaries.
/// Simplified (no single-chunk special casing) for maintainability.
/// </summary>
public ref struct PdfParseContext
{
    private readonly ReadOnlySpan<ReadOnlyMemory<byte>> _chunks;
    private readonly ReadOnlySpan<int> _chunkStartPositions; // Cumulative start offsets per chunk (last element == total length)
    private readonly int _length; // Total length across all chunks
    private int _currentChunkIndex;
    private int _currentChunkStart; // Absolute start position of current chunk
    private int _currentChunkEnd; // Absolute end (exclusive) position of current chunk
    private int _position; // Unified absolute position
    private ReadOnlySpan<byte> _currentChunk;
    private const int InvalidChunkIndex = -1;

    /// <summary>
    /// Construct context from a single memory block (wrapped as one chunk).
    /// </summary>
    /// <param name="memory">The memory block to parse.</param>
    public PdfParseContext(ReadOnlyMemory<byte> memory)
    {
        var chunkArray = memory.Length == 0 ? Array.Empty<ReadOnlyMemory<byte>>() : new[] { memory };
        _chunks = chunkArray;

        var starts = new int[chunkArray.Length + 1];
        var running = 0;
        for (var i = 0; i < chunkArray.Length; i++)
        {
            starts[i] = running;
            running += chunkArray[i].Length;
        }
        starts[chunkArray.Length] = running;
        _chunkStartPositions = starts;

        _length = running;
        _position = 0;
        _currentChunkIndex = chunkArray.Length > 0 ? 0 : InvalidChunkIndex;
        _currentChunk = chunkArray.Length > 0 ? chunkArray[0].Span : ReadOnlySpan<byte>.Empty;
        _currentChunkStart = _currentChunkIndex >= 0 ? 0 : 0;
        _currentChunkEnd = _currentChunkIndex >= 0 ? _currentChunk.Length : 0;
    }

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

        var chunkArray = chunks.ToArray();
        _chunks = chunkArray;

        var starts = new int[chunkArray.Length + 1];
        var running = 0;
        for (var i = 0; i < chunkArray.Length; i++)
        {
            starts[i] = running;
            running += chunkArray[i].Length;
        }
        starts[chunkArray.Length] = running;
        _chunkStartPositions = starts;

        _length = running;
        _position = 0;
        _currentChunkIndex = chunkArray.Length > 0 ? 0 : InvalidChunkIndex;
        _currentChunk = chunkArray.Length > 0 ? chunkArray[0].Span : ReadOnlySpan<byte>.Empty;
        _currentChunkStart = _currentChunkIndex >= 0 ? 0 : 0;
        _currentChunkEnd = _currentChunkIndex >= 0 ? _currentChunk.Length : 0;
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
            if (value > _length)
            {
                value = _length;
            }
            _position = value;
            SetCurrentChunkByPosition(value);
        }
    }

    /// <summary>
    /// Total length in bytes across all chunks.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _length; }
    }

    /// <summary>
    /// True if positioned at or beyond end of stream.
    /// </summary>
    public bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return _position >= _length; }
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
        var target = _position + offset;
        if (target < 0 || target >= _length)
        {
            return 0;
        }

        if (_currentChunkIndex >= 0 && target >= _currentChunkStart && target < _currentChunkEnd)
        {
            return _currentChunk[target - _currentChunkStart];
        }

        SetCurrentChunkByPosition(target);
        if (_currentChunkIndex < 0)
        {
            return 0;
        }

        return _currentChunk[target - _currentChunkStart];
    }

    /// <summary>
    /// Read a byte and advance position. Returns 0 at end.
    /// </summary>
    /// <returns>The byte value or 0 if at end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        var value = PeekByte();
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
        var newPosition = _position + count;
        if (newPosition < 0)
        {
            newPosition = 0;
        }
        if (newPosition > _length)
        {
            newPosition = _length;
        }
        _position = newPosition;
        SetCurrentChunkByPosition(newPosition);
    }

    /// <summary>
    /// Sets the current chunk based on an absolute position. If position is outside the stream, sets to empty.
    /// Fast path: if already inside current chunk, do nothing.
    /// </summary>
    /// <param name="position">Absolute position within the logical stream.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCurrentChunkByPosition(int position)
    {
        if (_currentChunkIndex >= 0 && position >= _currentChunkStart && position < _currentChunkEnd)
        {
            // Already within current chunk; nothing to update.
            return;
        }

        if (position < 0 || position >= _length)
        {
            _currentChunkIndex = InvalidChunkIndex;
            _currentChunk = ReadOnlySpan<byte>.Empty;
            _currentChunkStart = 0;
            _currentChunkEnd = 0;
            return;
        }

        int left = 0;
        int right = _chunks.Length - 1;
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            int chunkStart = _chunkStartPositions[mid];
            int chunkEnd = _chunkStartPositions[mid + 1];
            if (position >= chunkStart && position < chunkEnd)
            {
                _currentChunkIndex = mid;
                _currentChunkStart = chunkStart;
                _currentChunkEnd = chunkEnd;
                _currentChunk = _chunks[mid].Span;
                return;
            }
            if (position < chunkStart)
            {
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        _currentChunkIndex = InvalidChunkIndex;
        _currentChunk = ReadOnlySpan<byte>.Empty;
        _currentChunkStart = 0;
        _currentChunkEnd = 0;
    }

    public byte[] ToArray()
    {
        byte[] result = new byte[_length];
        int destOffset = 0;
        foreach (var chunk in _chunks)
        {
            var span = chunk.Span;
            span.CopyTo(result.AsSpan(destOffset, span.Length));
            destOffset += span.Length;
        }
        return result;
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();

        foreach (var chunk in _chunks)
        {
            builder.Append(Text.EncodingExtensions.PdfDefault.GetString(chunk));
        }

        return builder.ToString();
    }
}