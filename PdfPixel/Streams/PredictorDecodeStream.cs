using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Streams;

/// <summary>
/// Stream that undoes the TIFF (2) and PNG (10..15) predictors one row at a time.
/// Supports 1, 2, 4, 8 and 16 bits per component.
/// </summary>
public sealed class PredictorDecodeStream : Stream
{
    private readonly Stream _source;
    private readonly int _predictor;
    private readonly int _colors;
    private readonly int _bitsPerComponent;
    private readonly int _columns;
    private readonly bool _leaveOpen;

    private readonly int _bytesPerSample;
    private readonly int _decodedRowBytes;
    private readonly int _encodedRowBytes;

    // TIFF layout: [row data]. PNG layout: [margin bytes][filter byte][row data].
    private readonly byte[] _currentRow;
    private readonly byte[]? _previousRow;

    private readonly int _rowMarginBytes;
    private readonly int _rowDataOffset;

    private int _rowOffset;
    private bool _endOfStream;
    private bool _currentRowValid;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Initializes the decoder wrapping the given filter-decoded stream.
    /// </summary>
    public PredictorDecodeStream(Stream decoded, int predictor, int colors, int bitsPerComponent, int columns, bool leaveOpen = false)
    {
        if (decoded == null)
        {
            throw new ArgumentNullException(nameof(decoded));
        }

        if (colors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(colors));
        }

        if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
        {
            throw new NotSupportedException("Only 1,2,4,8 or 16 bits per component predictors are supported.");
        }

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        _leaveOpen = leaveOpen;

        // Identity path for unsupported predictor values.
        if (predictor != 2 && (predictor < 10 || predictor > 15))
        {
            _source = decoded;
            _predictor = 1;
            _colors = colors;
            _bitsPerComponent = bitsPerComponent;
            _columns = columns;
            _bytesPerSample = (bitsPerComponent >= 8) ? (bitsPerComponent + 7) / 8 : 1;
            _decodedRowBytes = (bitsPerComponent >= 8) ? columns * colors * _bytesPerSample : ((columns * colors * bitsPerComponent) + 7) / 8;
            _encodedRowBytes = _decodedRowBytes;
            _rowMarginBytes = 0;
            _rowDataOffset = 0;
            _currentRow = Array.Empty<byte>();
            _previousRow = null;
            _rowOffset = 0;
            _endOfStream = false;
            _currentRowValid = false;
            return;
        }

        _source = decoded;
        _predictor = predictor;
        _colors = colors;
        _bitsPerComponent = bitsPerComponent;
        _columns = columns;
        _bytesPerSample = (bitsPerComponent >= 8) ? (bitsPerComponent + 7) / 8 : 1;
        _decodedRowBytes = (bitsPerComponent >= 8) ? columns * colors * _bytesPerSample : ((columns * colors * bitsPerComponent) + 7) / 8;
        _encodedRowBytes = (predictor >= 10) ? _decodedRowBytes + 1 : _decodedRowBytes;

        if (predictor >= 10)
        {
            int bytesPerPixel = ((_colors * _bitsPerComponent) + 7) / 8;
            _rowMarginBytes = bytesPerPixel;
            _rowDataOffset = _rowMarginBytes + 1;
            int total = _rowMarginBytes + 1 + _decodedRowBytes;
            _currentRow = new byte[total];
            _previousRow = new byte[total];
        }
        else
        {
            _rowMarginBytes = 0;
            _rowDataOffset = 0;
            _currentRow = new byte[_decodedRowBytes];
            _previousRow = null;
        }

        _rowOffset = 0;
        _endOfStream = false;
        _currentRowValid = false;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count == 0)
        {
            return 0;
        }

        int totalCopied = 0;
        while (count > 0)
        {
            if (!_currentRowValid || _rowOffset >= _decodedRowBytes)
            {
                if (_endOfStream)
                {
                    break;
                }

                if (!DecodeNextRow())
                {
                    break;
                }
            }

            int remainingInRow = _decodedRowBytes - _rowOffset;
            int toCopy = (remainingInRow < count) ? remainingInRow : count;
            Array.Copy(_currentRow, _rowDataOffset + _rowOffset, buffer, offset, toCopy);
            _rowOffset += toCopy;
            offset += toCopy;
            count -= toCopy;
            totalCopied += toCopy;
        }

        return totalCopied;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool DecodeNextRow()
    {
        _rowOffset = 0;
        byte filterByte = 0;

        if (_predictor >= 10)
        {
            int start = _rowMarginBytes;
            int needed = _encodedRowBytes;
            int readOffset = 0;
            while (readOffset < needed)
            {
                int read = _source.Read(_currentRow, start + readOffset, needed - readOffset);
                if (read <= 0)
                {
                    _endOfStream = true;
                    if (readOffset == 0)
                    {
                        _currentRowValid = false;
                        return false;
                    }

                    _currentRowValid = true; // partial
                    return true;
                }

                readOffset += read;
            }

            filterByte = _currentRow[start];
        }
        else
        {
            int needed = _decodedRowBytes;
            int readOffset = 0;
            while (readOffset < needed)
            {
                int read = _source.Read(_currentRow, readOffset, needed - readOffset);
                if (read <= 0)
                {
                    _endOfStream = true;
                    if (readOffset == 0)
                    {
                        _currentRowValid = false;
                        return false;
                    }

                    _currentRowValid = true; // partial
                    return true;
                }

                readOffset += read;
            }
        }

        if (_predictor == 2)
        {
            TiffPredictorUndo.UndoTiffPredictor(_currentRow, _columns, _colors, _bitsPerComponent, _bytesPerSample);
        }
        else if (_predictor >= 10 && _predictor <= 15)
        {
            if (_previousRow == null)
            {
                throw new ArgumentNullException(nameof(_previousRow));
            }

            PngFilterUndo.UndoPngFilter(filterByte, _currentRow, _previousRow, _rowMarginBytes, _rowDataOffset, _decodedRowBytes);
            Buffer.BlockCopy(_currentRow, _rowDataOffset, _previousRow, _rowDataOffset, _decodedRowBytes);
        }

        _currentRowValid = true;
        return true;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _source.Dispose();
        }

        base.Dispose(disposing);
    }
}
