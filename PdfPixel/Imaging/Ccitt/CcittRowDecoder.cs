using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Ccitt;

/// <summary>
/// Stateful row decoder for CCITT Fax (CCITTFaxDecode) compressed bi-level images.
/// Supports Group 3 1-D (K == 0), Group 3 / Group 4 2-D (K &lt; 0) and mixed 1-D / 2-D modes (K &gt; 0).
/// Produces packed 1-bit rows (MSB first per byte) honoring <c>BlackIs1</c> polarity.
/// Each call to <see cref="DecodeNextRow"/> decodes exactly one raster row until all rows are exhausted.
/// This class snapshots bit reader state between rows instead of holding a ref struct field.
/// </summary>
internal sealed class CcittRowDecoder
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
    private int _bitsRemaining;
    private byte _currentByte;

    private readonly int[] _referenceChanges;
    private int _changesCount;
    private readonly List<int> _runs;

    private int _currentRowIndex;
    private bool _completed;
    private bool _rtcConsumed;

    public CcittRowDecoder(ReadOnlySpan<byte> encodedData,
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

        _encoded = encodedData.ToArray(); // store a copy for safe span usage
        _width = width;
        _height = height;
        _blackIs1 = blackIs1;
        _kParameter = k;
        _endOfLine = endOfLine;
        _byteAlign = byteAlign;
        _endOfBlock = endOfBlock;

        _byteIndex = 0;
        _bitsRemaining = 0;
        _currentByte = 0;

        _runs = new List<int>(256);
        _referenceChanges = new int[_width + 1];
        _referenceChanges[0] = _width;
        _changesCount = 1;
        _currentRowIndex = 0;
        _completed = false;
        _rtcConsumed = false;
    }

    public int RowStride
    {
        get { return (_width + 7) / 8; }
    }

    public int RowsDecoded
    {
        get { return _currentRowIndex; }
    }

    public bool IsCompleted
    {
        get { return _completed; }
    }

    public bool DecodeNextRow(Span<byte> destinationRow)
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
        var reader = new CcittBitReader(_encoded.Span, _byteIndex, _bitsRemaining, _currentByte);

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
        _bitsRemaining = reader.BitsRemaining;
        _currentByte = reader.Current;

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

    private void CleanupBuffer(Span<byte> rowSpan)
    {
        byte backgroundByte = _blackIs1 ? (byte)0x00 : (byte)0xFF;

        for (int i = 0; i < rowSpan.Length; i++)
        {
            rowSpan[i] = backgroundByte;
        }
    }

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

    private bool ConsumeEolOptional(ref CcittBitReader reader)
    {
        return reader.TryConsumeEol();
    }
}
