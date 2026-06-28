using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Ccitt;

/// <summary>
/// Stateful row decoder for CCITT Fax (CCITTFaxDecode) compressed bi-level images.
/// Supports Group 3 1-D (K == 0), Group 3 / Group 4 2-D (K &lt; 0) and mixed 1-D / 2-D modes (K &gt; 0).
/// Produces packed 1-bit rows (MSB first per byte) honoring <c>BlackIs1</c> polarity.
/// Each call to <see cref="DecodeNextRow"/> decodes exactly one raster row until all rows are exhausted.
/// This class snapshots bit reader state between rows instead of holding a ref struct field.
/// </summary>
public sealed class CcittRowDecoder
{
    private readonly int _width;
    private readonly int _height;
    private readonly bool _blackIs1;
    private readonly int _kParameter;
    private readonly bool _endOfLine;
    private readonly bool _byteAlign;
    private readonly bool _endOfBlock;

    private readonly ReadOnlyMemory<byte> _encoded; // store original encoded data

    // Bit reader state persisted between rows
    private int _byteIndex;
    private int _bufferedBits;
    private ulong _buffer;

    private readonly int[] _referenceChanges;
    private int _changesCount;
    private readonly List<int> _runs;

    private int _currentRowIndex;
    private bool _completed;
    private bool _rtcConsumed;

    /// <summary>
    /// Initializes the decoder with image dimensions and all CCITT decode parameters.
    /// </summary>
    /// <param name="encodedData">The raw CCITT-encoded byte stream.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Number of rows to decode.</param>
    /// <param name="blackIs1">When true, bit value 1 represents black; otherwise 0 represents black.</param>
    /// <param name="k">K parameter: 0 = G3 1-D only, negative = G4 2-D only, positive = mixed 1-D/2-D with K rows per sync.</param>
    /// <param name="endOfLine">When true, each row is preceded by an EOL marker.</param>
    /// <param name="byteAlign">When true, the stream is byte-aligned after each EOL marker.</param>
    /// <param name="endOfBlock">When true, an RTC (six EOLs) is expected and consumed at the end of the image.</param>
    public CcittRowDecoder(
        in ReadOnlyMemory<byte> encodedData,
        int width,
        int height,
        bool blackIs1,
        int k,
        bool endOfLine,
        bool byteAlign,
        bool endOfBlock)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        _encoded = encodedData;
        _width = width;
        _height = height;
        _blackIs1 = blackIs1;
        _kParameter = k;
        _endOfLine = endOfLine;
        _byteAlign = byteAlign;
        _endOfBlock = endOfBlock;

        _byteIndex = 0;
        _bufferedBits = 0;
        _buffer = 0;

        _runs = new List<int>(256);
        _referenceChanges = new int[_width + 1];
        _referenceChanges[0] = _width;
        _changesCount = 1;
        _currentRowIndex = 0;
        _completed = false;
        _rtcConsumed = false;
    }

    /// <summary>
    /// Byte length of each output row: <c>(Width + 7) / 8</c>.
    /// </summary>
    public int RowStride => (_width + 7) / 8;

    /// <summary>
    /// Number of rows successfully decoded so far.
    /// </summary>
    public int RowsDecoded => _currentRowIndex;

    /// <summary>
    /// True once all rows have been decoded (or decoding has been aborted).
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Decodes the next raster row into <paramref name="destinationRow"/>.
    /// </summary>
    /// <param name="destinationRow">Output buffer; must be at least <see cref="RowStride"/> bytes long.</param>
    /// <returns>True if a row was decoded; false when all rows are exhausted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DecodeNextRow(ref readonly Span<byte> destinationRow)
    {
        if (_completed)
        {
            return false;
        }

        if (destinationRow.Length < RowStride)
        {
            throw new ArgumentException("Destination span too small for row stride.", nameof(destinationRow));
        }

        if (_currentRowIndex >= _height)
        {
            _completed = true;
            return false;
        }

        // Reconstruct reader for current state
        ReadOnlySpan<byte> encodedSpan = _encoded.Span;
        CcittBitReader reader = new(ref encodedSpan, _byteIndex, _bufferedBits, _buffer);

        bool isOneDLine = DetermineLineKind(ref reader);

        _runs.Clear();

        if (isOneDLine)
        {
            CcittG3OneDDecoder.DecodeOneDCollectRuns(ref reader, _width, requireLeadingEol: false, byteAlign: false, runs: _runs);
        }
        else
        {
            CcittG4TwoDDecoder.DecodeTwoDLine(ref reader, _width, _referenceChanges.AsSpan().Slice(0, _changesCount), _runs);
        }

        Span<byte> rowSpan = destinationRow.Slice(0, RowStride);

        CleanupBuffer(rowSpan);

        CcittRaster.RasterizeRuns(rowSpan, _runs, 0, _width, _blackIs1);

        _changesCount = CcittRaster.BuildReferenceChangeList(_runs, _width, _referenceChanges);

        // Snapshot updated reader state
        _byteIndex = reader.ByteIndex;
        _bufferedBits = reader.BufferedBits;
        _buffer = reader.Buffer;

        _currentRowIndex++;
        if (_currentRowIndex >= _height)
        {
            if (_kParameter < 0 && _endOfBlock && !_rtcConsumed)
            {
                reader.TryConsumeRtc();
                _rtcConsumed = true;
            }

            _completed = true;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanupBuffer(in Span<byte> rowSpan)
    {
        byte backgroundByte = _blackIs1 ? (byte)0x00 : (byte)0xFF;

        for (int i = 0; i < rowSpan.Length; i++)
        {
            rowSpan[i] = backgroundByte;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool DetermineLineKind(ref CcittBitReader reader)
    {
        if (_kParameter == 0)
        {
            if (_endOfLine)
            {
                ConsumeMandatoryEol(ref reader);
            }

            return true;
        }

        if (_kParameter < 0)
        {
            if (_endOfLine)
            {
                ConsumeMandatoryEol(ref reader);
            }

            return false;
        }

        if (!ConsumeEolOptional(ref reader))
        {
            throw new InvalidOperationException("CCITT mixed mode decode error: missing EOL before tag bit at row " + _currentRowIndex + ".");
        }

        if (_byteAlign)
        {
            reader.AlignAfterEndOfLine(true);
        }

        int tagBit = reader.ReadBit();

        if (tagBit < 0)
        {
            throw new InvalidOperationException("CCITT mixed mode decode error: unexpected end of data reading tag bit at row " + _currentRowIndex + ".");
        }

        return tagBit == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConsumeMandatoryEol(ref CcittBitReader reader)
    {
        if (!ConsumeEolOptional(ref reader))
        {
            throw new InvalidOperationException("CCITT decode error: missing required EOL at row " + _currentRowIndex + ".");
        }

        if (_byteAlign)
        {
            reader.AlignAfterEndOfLine(true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ConsumeEolOptional(ref CcittBitReader reader) => reader.TryConsumeEol();
}
