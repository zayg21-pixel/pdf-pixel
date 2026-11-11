using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// Reads a window of columns of an image source's sample rows from its decoder's tiles, packed from bit 0, every bit
/// complemented when the reader inverts its samples.
/// </summary>
internal sealed class SnapImageReader : IDisposable
{
    private readonly IPdfImageDecoder _decoder;
    private readonly bool _readsAlpha;
    private readonly bool _invertsSamples;
    private readonly PdfTileInfo _grid;
    private readonly IPdfExecutionObserver _observer;
    private readonly int _columnStart;
    private readonly int _columnEnd;
    private readonly int _pixelBits;
    private readonly int _firstTileColumn;
    private readonly PdfImageTile?[] _tiles;
    private readonly SnapMemoryBlock _row;
    private int _tileRow = -1;

    /// <summary>
    /// Initializes a reader of <paramref name="columnCount"/> columns from <paramref name="columnStart"/>, its row
    /// rented from <paramref name="memoryPool"/> until <see cref="Dispose"/>.
    /// </summary>
    public SnapImageReader(
        IPdfImageDecoder decoder,
        bool readsAlpha,
        bool invertsSamples,
        PdfImageSampleInfo samples,
        IPdfExecutionObserver observer,
        int columnStart,
        int columnCount,
        PdfSnapMemoryPool memoryPool)
    {
        _decoder = decoder;
        _readsAlpha = readsAlpha;
        _invertsSamples = invertsSamples;
        _grid = samples.Grid;
        _observer = observer;
        _columnStart = columnStart;
        _columnEnd = columnStart + columnCount;
        _pixelBits = samples.ComponentCount * samples.BitsPerComponent;
        _firstTileColumn = columnStart / _grid.TileWidth;
        _tiles = new PdfImageTile?[((_columnEnd - 1) / _grid.TileWidth) - _firstTileColumn + 1];
        RowLength = ((columnCount * _pixelBits) + 7) / 8;
        _row = memoryPool.Rent(RowLength);
    }

    /// <summary>
    /// Gets the number of bytes of a row read.
    /// </summary>
    public int RowLength { get; }

    /// <summary>
    /// Returns the window of sample row <paramref name="row"/>, valid until the next read; tiles that cannot be
    /// produced read as zero.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public ReadOnlySpan<byte> ReadRow(int row)
    {
        int tileRow = row / _grid.TileHeight;
        Span<byte> rowBytes = _row.Array.AsSpan(0, RowLength);

        if (tileRow != _tileRow)
        {
            LoadTiles(tileRow);
        }

        for (int index = 0; index < _tiles.Length; index++)
        {
            int tileColumn = _firstTileColumn + index;
            int columnStart = Math.Max(_columnStart, tileColumn * _grid.TileWidth);
            int columnEnd = Math.Min(_columnEnd, (tileColumn + 1) * _grid.TileWidth);
            int destinationBit = (columnStart - _columnStart) * _pixelBits;
            int bitCount = (columnEnd - columnStart) * _pixelBits;
            PdfImageTile? tile = _tiles[index];

            if (tile == null)
            {
                ClearBits(rowBytes, destinationBit, bitCount);
                continue;
            }

            PdfImageTile view = tile.Value;

            CopyBits(view.GetRow(row), (columnStart - view.ColumnOrigin) * _pixelBits, rowBytes, destinationBit, bitCount);
        }

        if (_invertsSamples)
        {
            InvertBits(rowBytes);
        }

        return rowBytes;
    }

    /// <summary>
    /// Complements every bit of <paramref name="row"/>, eight bytes at a time.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void InvertBits(in Span<byte> row)
    {
        Span<ulong> words = MemoryMarshal.Cast<byte, ulong>(row);

        for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            words[wordIndex] = ~words[wordIndex];
        }

        for (int byteIndex = words.Length * sizeof(ulong); byteIndex < row.Length; byteIndex++)
        {
            row[byteIndex] = (byte)~row[byteIndex];
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void LoadTiles(int tileRow)
    {
        for (int index = 0; index < _tiles.Length; index++)
        {
            int tileIndex = _grid.GetTileIndex(_firstTileColumn + index, tileRow);

            if (_readsAlpha)
            {
                _tiles[index] = _decoder.GetAlphaTile(tileIndex, _observer);
            }
            else
            {
                _tiles[index] = _decoder.GetTile(tileIndex, _observer);
            }
        }

        _tileRow = tileRow;
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void CopyBits(in ReadOnlySpan<byte> source, int sourceBit, in Span<byte> destination, int destinationBit, int bitCount)
    {
        if ((sourceBit & 7) == 0 && (destinationBit & 7) == 0)
        {
            int byteCount = bitCount >> 3;

            source.Slice(sourceBit >> 3, byteCount).CopyTo(destination.Slice(destinationBit >> 3, byteCount));
            sourceBit += byteCount << 3;
            destinationBit += byteCount << 3;
            bitCount -= byteCount << 3;
        }

        while (bitCount > 0)
        {
            int destinationOffset = destinationBit & 7;
            int count = Math.Min(8 - destinationOffset, bitCount);
            int shift = 8 - destinationOffset - count;
            int mask = ((1 << count) - 1) << shift;
            int value = ReadBits(source, sourceBit, count) << shift;
            int destinationByte = destinationBit >> 3;

            destination[destinationByte] = (byte)((destination[destinationByte] & ~mask) | value);
            sourceBit += count;
            destinationBit += count;
            bitCount -= count;
        }
    }

    private static int ReadBits(in ReadOnlySpan<byte> source, int bit, int count)
    {
        int byteIndex = bit >> 3;
        int window = source[byteIndex] << 8;

        if (byteIndex + 1 < source.Length)
        {
            window |= source[byteIndex + 1];
        }

        return (window >> (16 - (bit & 7) - count)) & ((1 << count) - 1);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void ClearBits(in Span<byte> destination, int destinationBit, int bitCount)
    {
        while (bitCount > 0)
        {
            int destinationOffset = destinationBit & 7;
            int count = Math.Min(8 - destinationOffset, bitCount);
            int mask = ((1 << count) - 1) << (8 - destinationOffset - count);
            int destinationByte = destinationBit >> 3;

            destination[destinationByte] = (byte)(destination[destinationByte] & ~mask);
            destinationBit += count;
            bitCount -= count;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _row.Dispose();
}
