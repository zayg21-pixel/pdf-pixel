using System;
using System.Runtime.CompilerServices;
using PdfPixel.Imaging.Jbig2.Model;

namespace PdfPixel.Imaging.Jbig2.Decoding;

/// <summary>
/// A lightweight sequential horizontal bit writer over a <see cref="Jbig2Bitmap"/>.
/// Caches a single byte and writes bits from MSB to LSB, flushing to the backing
/// data when 8 bits accumulate or when <see cref="Flush"/> is called.
/// Designed for compositing and row-level write operations.
/// </summary>
internal ref struct BitmapRowBitWriter
{
    private readonly Span<byte> _data;
    private readonly int _stride;
    private int _byteIndex;
    private int _cachedByte;
    private int _bitsWritten;

    /// <summary>
    /// Initializes a writer over the specified bitmap.
    /// </summary>
    /// <param name="bitmap">The target bitmap to write to.</param>
    public BitmapRowBitWriter(Jbig2Bitmap bitmap)
    {
        _data = bitmap.Data;
        _stride = bitmap.Stride;
        _byteIndex = 0;
        _cachedByte = 0;
        _bitsWritten = 0;
    }

    /// <summary>
    /// Positions the writer at the specified row and column for subsequent horizontal writes.
    /// If the column is not byte-aligned, loads the existing byte and skips leading bits.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index (must be non-negative and within bounds).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToPosition(int row, int column)
    {
        int rowOffset = row * _stride;
        _byteIndex = rowOffset + (column >> 3);
        int bitOffset = column & 7;

        if (bitOffset == 0)
        {
            _cachedByte = 0;
            _bitsWritten = 0;
        }
        else
        {
            // Load existing byte and preserve leading bits
            _cachedByte = _data[_byteIndex] >> (8 - bitOffset);
            _bitsWritten = bitOffset;
        }
    }

    /// <summary>
    /// Writes a single bit at the current position and advances one pixel to the right.
    /// </summary>
    /// <param name="bit">The pixel value (0 or 1).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBit(int bit)
    {
        _cachedByte = (_cachedByte << 1) | bit;
        _bitsWritten++;

        if (_bitsWritten == 8)
        {
            _data[_byteIndex] = (byte)_cachedByte;
            _byteIndex++;
            _cachedByte = 0;
            _bitsWritten = 0;
        }
    }

    /// <summary>
    /// Flushes any remaining bits in the cache to the backing data.
    /// Preserves trailing bits in the destination byte that are beyond the written region.
    /// Must be called after the last <see cref="WriteBit"/> if the total count is not a multiple of 8.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush()
    {
        if (_bitsWritten > 0)
        {
            int shift = 8 - _bitsWritten;
            int existingBits = _data[_byteIndex] & ((1 << shift) - 1);
            _data[_byteIndex] = (byte)((_cachedByte << shift) | existingBits);
        }
    }
}
