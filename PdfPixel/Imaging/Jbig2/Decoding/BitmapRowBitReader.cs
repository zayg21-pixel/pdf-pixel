using System;
using System.Runtime.CompilerServices;
using PdfPixel.Imaging.Jbig2.Model;

namespace PdfPixel.Imaging.Jbig2.Decoding;

/// <summary>
/// A lightweight sequential horizontal bit reader over a <see cref="Jbig2Bitmap"/>.
/// Caches a single byte and reads bits from MSB to LSB using a shifting mask.
/// Designed for the hot-path of JBIG2 context-building where pixels are read left-to-right.
/// </summary>
/// <remarks>
/// Out-of-bounds positions (negative coordinates or beyond dimensions) return 0,
/// matching JBIG2 spec behavior for context pixels outside the coded region.
/// </remarks>
internal ref struct BitmapRowBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private int _column;
    private int _rowOffset;
    private int _cachedByte;
    private int _bitsRemaining;
    private int _byteIndex;

    /// <summary>
    /// Initializes a reader over the specified bitmap.
    /// </summary>
    /// <param name="bitmap">The source bitmap to read from.</param>
    public BitmapRowBitReader(Jbig2Bitmap bitmap)
    {
        _data = bitmap.ReadOnlyData;
        _width = bitmap.Width;
        _height = bitmap.Height;
        _stride = bitmap.Stride;
        _column = 0;
        _rowOffset = 0;
        _cachedByte = 0;
        _bitsRemaining = 0;
        _byteIndex = 0;
    }

    /// <summary>
    /// Positions the reader at the specified row and column for subsequent horizontal reads.
    /// </summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index (may be negative for out-of-bounds prefill).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToPosition(int row, int column)
    {
        _column = column;
        _bitsRemaining = 0;

        if ((uint)row >= (uint)_height)
        {
            _rowOffset = -1;
            return;
        }

        _rowOffset = row * _stride;

        if (column >= 0)
        {
            _byteIndex = _rowOffset + (column >> 3);
            _cachedByte = _data[_byteIndex];
            _byteIndex++;
            // Position within the byte: discard bits before our column
            int bitOffset = column & 7;
            _cachedByte <<= bitOffset;
            _bitsRemaining = 8 - bitOffset;
        }
        else
        {
            _byteIndex = _rowOffset;
        }
    }

    /// <summary>
    /// Reads the bit at the current position and advances one pixel to the right.
    /// Shifts one bit off the cached byte; loads next byte when exhausted.
    /// Returns 0 for out-of-bounds positions.
    /// </summary>
    /// <returns>The pixel value (0 or 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadHorizontalBit()
    {
        if (_rowOffset < 0 || (uint)_column++ >= (uint)_width)
        {
            return 0;
        }

        if (_bitsRemaining == 0)
        {
            _cachedByte = _data[_byteIndex];
            _byteIndex++;
            _bitsRemaining = 8;
        }

        int bit = (_cachedByte >> 7) & 1;
        _cachedByte <<= 1;
        _bitsRemaining--;
        return bit;
    }
}
