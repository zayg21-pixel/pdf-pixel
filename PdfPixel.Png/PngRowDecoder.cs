using PdfPixel.Png.Decoding;
using PdfPixel.Png.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace PdfPixel.Png;

/// <summary>
/// Decodes a PNG image row by row into interleaved samples of <see cref="BitsPerComponent"/> bits, packed MSB-first
/// with 16-bit samples big-endian. Indexed-color images produce palette indices.
/// </summary>
public sealed class PngRowDecoder
{
    private const int MaxFilterType = 4;

    private readonly DeflateStream _inflater;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly int _bitsPerPixel;
    private readonly int _filterBytesPerPixel;
    private readonly int _sourceRowBytes;
    private readonly int _descaleFactor;
    private readonly bool _isInterlaced;

    /// <summary>
    /// Rows of an interlaced image taken from the passes before the last one needed: every second row at full
    /// size, every <see cref="_descaleFactor"/>-th row when descaled. Null for a non-interlaced image.
    /// </summary>
    private readonly byte[]? _storedRows;

    private readonly int _storedRowStep;

    private byte[] _currentRow;
    private byte[] _previousRow;
    private int _nextSourceRow;
    private bool _arePassesLoaded;

    /// <summary>
    /// Initializes a decoder for the image a PNG file holds.
    /// </summary>
    /// <param name="info">Image info read from the file.</param>
    /// <param name="data">Complete PNG file the info was read from.</param>
    /// <param name="descaleFactor">Reduction of the decoded size: 1, 2, 4 or 8. A reduced image holds every
    /// <paramref name="descaleFactor"/>-th pixel of every <paramref name="descaleFactor"/>-th row.</param>
    /// <exception cref="InvalidOperationException"><paramref name="info"/> was not read from a file.</exception>
    public PngRowDecoder(PngImageInfo info, in ReadOnlyMemory<byte> data, int descaleFactor = 1)
    {
        if (info == null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        if (descaleFactor != 1 && descaleFactor != 2 && descaleFactor != 4 && descaleFactor != 8)
        {
            throw new ArgumentOutOfRangeException(nameof(descaleFactor), "Descale factor must be 1, 2, 4 or 8.");
        }

        List<PngDataSegment> imageData = info.ImageData
            ?? throw new InvalidOperationException("PNG image info was not read from a file and has no image data.");

        PngLayout.ValidateHeader(info.Width, info.Height, info.BitDepth, info.ColorType);

        _sourceWidth = info.Width;
        _sourceHeight = info.Height;
        _descaleFactor = descaleFactor;
        _isInterlaced = info.InterlaceMethod == PngInterlaceMethod.Adam7;

        ColorType = info.ColorType;
        BitsPerComponent = info.BitDepth;
        ComponentCount = PngLayout.GetChannelCount(info.ColorType);
        Width = (_sourceWidth + descaleFactor - 1) / descaleFactor;
        Height = (_sourceHeight + descaleFactor - 1) / descaleFactor;

        _bitsPerPixel = PngLayout.GetBitsPerPixel(info.BitDepth, info.ColorType);
        _filterBytesPerPixel = PngLayout.GetFilterBytesPerPixel(_bitsPerPixel);
        _sourceRowBytes = PngLayout.GetRowBytes(_sourceWidth, _bitsPerPixel);
        RowBytes = PngLayout.GetRowBytes(Width, _bitsPerPixel);

        _currentRow = new byte[_sourceRowBytes + 1];
        _previousRow = new byte[_sourceRowBytes + 1];

        if (_isInterlaced)
        {
            _storedRowStep = (descaleFactor == 1) ? 2 : descaleFactor;
            int storedRowCount = (_sourceHeight + _storedRowStep - 1) / _storedRowStep;
            _storedRows = new byte[(long)storedRowCount * RowBytes];
        }

        _inflater = PngZlibDecoder.Open(new PngSegmentStream(data, imageData));
    }

    /// <summary>
    /// Width of the produced image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the produced image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Bit depth of every produced sample.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Number of samples per pixel.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Byte length of each produced row.
    /// </summary>
    public int RowBytes { get; }

    /// <summary>
    /// Samples each produced pixel carries.
    /// </summary>
    public PngColorType ColorType { get; }

    /// <summary>
    /// Zero-based index of the next row to be produced.
    /// </summary>
    public int CurrentRow { get; private set; }

    /// <summary>
    /// Reads the next image row into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">Row buffer, at least <see cref="RowBytes"/> long.</param>
    /// <returns>True when a row was written; false once every row has been produced.</returns>
    /// <exception cref="InvalidDataException">The image data is truncated or damaged.</exception>
    public bool TryReadRow(in Span<byte> destination)
    {
        if (CurrentRow >= Height)
        {
            return false;
        }

        if (destination.Length < RowBytes)
        {
            throw new ArgumentException($"Row buffer holds {destination.Length} bytes, expected {RowBytes}.", nameof(destination));
        }

        Span<byte> row = destination.Slice(0, RowBytes);

        if (_storedRows == null)
        {
            ReadNonInterlacedRow(row);
        }
        else
        {
            ReadInterlacedRow(_storedRows, row);
        }

        CurrentRow++;

        if (CurrentRow == Height)
        {
            _inflater.Dispose();
        }

        return true;
    }

    private void ReadNonInterlacedRow(in Span<byte> destination)
    {
        int sourceRow = CurrentRow * _descaleFactor;

        while (_nextSourceRow <= sourceRow)
        {
            ReadFilteredRow(_sourceRowBytes);
            _nextSourceRow++;
        }

        ReadOnlySpan<byte> decoded = _previousRow.AsSpan(1, _sourceRowBytes);

        if (_descaleFactor == 1)
        {
            decoded.CopyTo(destination);
        }
        else
        {
            destination.Clear();
            Subsample(decoded, destination);
        }
    }

    private void ReadInterlacedRow(byte[] storedRows, in Span<byte> destination)
    {
        if (!_arePassesLoaded)
        {
            LoadPasses(storedRows);
            _arePassesLoaded = true;
        }

        if (_descaleFactor > 1 || (CurrentRow & 1) == 0)
        {
            int storedRow = (_descaleFactor > 1) ? CurrentRow : CurrentRow >> 1;
            storedRows.AsSpan(storedRow * RowBytes, RowBytes).CopyTo(destination);
            return;
        }

        // Pass 7 holds every odd row at full width.
        ReadFilteredRow(_sourceRowBytes);
        _previousRow.AsSpan(1, _sourceRowBytes).CopyTo(destination);
    }

    /// <summary>
    /// Reads the passes whose pixels fall on the stored rows: passes 1 to 6 at full size, which cover every even
    /// row, or the passes covering every <see cref="_descaleFactor"/>-th pixel of every
    /// <see cref="_descaleFactor"/>-th row.
    /// </summary>
    private void LoadPasses(byte[] storedRows)
    {
        int passCount = _descaleFactor switch
        {
            8 => 1,
            4 => 3,
            2 => 5,
            _ => PngAdam7.PassCount - 1
        };

        for (int pass = 0; pass < passCount; pass++)
        {
            ReadPass(pass, storedRows);
        }

        Array.Clear(_previousRow, 0, _previousRow.Length);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void ReadPass(int pass, byte[] storedRows)
    {
        int passWidth = PngAdam7.GetPassWidth(pass, _sourceWidth);
        int passHeight = PngAdam7.GetPassHeight(pass, _sourceHeight);

        if (passWidth == 0 || passHeight == 0)
        {
            return;
        }

        int passRowBytes = PngLayout.GetRowBytes(passWidth, _bitsPerPixel);
        int startX = PngAdam7.StartX[pass];
        int stepX = PngAdam7.StepX[pass];

        Array.Clear(_previousRow, 0, passRowBytes + 1);

        for (int passRow = 0; passRow < passHeight; passRow++)
        {
            ReadFilteredRow(passRowBytes);

            int sourceRow = PngAdam7.StartY[pass] + (passRow * PngAdam7.StepY[pass]);
            Span<byte> storedRow = storedRows.AsSpan(sourceRow / _storedRowStep * RowBytes, RowBytes);
            ReadOnlySpan<byte> decoded = _previousRow.AsSpan(1, passRowBytes);

            for (int pixel = 0; pixel < passWidth; pixel++)
            {
                int sourceColumn = startX + (pixel * stepX);
                PngPixelCopy.Copy(decoded, pixel, storedRow, sourceColumn / _descaleFactor, _bitsPerPixel);
            }
        }
    }

    /// <summary>
    /// Reads one filtered row of <paramref name="rowBytes"/> bytes, undoes its filter against the previous row,
    /// and leaves the result in <see cref="_previousRow"/> after the filter type byte.
    /// </summary>
    private void ReadFilteredRow(int rowBytes)
    {
        int length = rowBytes + 1;
        int offset = 0;

        while (offset < length)
        {
            int read = _inflater.Read(_currentRow, offset, length - offset);
            if (read <= 0)
            {
                throw new InvalidDataException("PNG image data ends before the last row.");
            }

            offset += read;
        }

        byte filterType = _currentRow[0];
        if (filterType > MaxFilterType)
        {
            throw new InvalidDataException($"PNG row uses the undefined filter type {filterType}.");
        }

        PngFilterUndo.UndoPngFilter(filterType, _currentRow, _previousRow, _filterBytesPerPixel, 1, rowBytes);

        (_previousRow, _currentRow) = (_currentRow, _previousRow);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void Subsample(in ReadOnlySpan<byte> source, in Span<byte> destination)
    {
        for (int pixel = 0; pixel < Width; pixel++)
        {
            PngPixelCopy.Copy(source, pixel * _descaleFactor, destination, pixel, _bitsPerPixel);
        }
    }
}
