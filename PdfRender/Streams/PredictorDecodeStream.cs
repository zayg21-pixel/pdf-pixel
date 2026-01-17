using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfRender.Streams
{
    /// <summary>
    /// Streaming predictor undo wrapper for TIFF (2) and PNG (10..15) predictors.
    /// Decodes one row at a time (no full buffering). Supports bits per component 1,2,4,8,16.
    /// Sub-byte sample packing is preserved; predictor undo operates in packed form for TIFF.
    /// </summary>
    internal sealed class PredictorDecodeStream : Stream
    {
        // Underlying decoded (filter chain already applied) stream provided by caller.
        private readonly Stream _source;
        // Predictor value from PDF image dictionary: 2 (TIFF) or 10..15 (PNG filters). 1 treated as identity.
        private readonly int _predictor;
        // Number of color components per pixel (samples per pixel).
        private readonly int _colors;
        // Bits per component (sample size) 1,2,4,8 or 16.
        private readonly int _bitsPerComponent;
        // Pixel width (columns) of the image row.
        private readonly int _columns;
        // Whether to leave underlying stream open when disposing.
        private readonly bool _leaveOpen;

        // Bytes per sample (1 for <=8 bpc, 2 for 16 bpc). For sub‑byte packing still 1 here.
        private readonly int _bytesPerSample;
        // Logical decoded bytes in a row (packed for sub‑byte samples).
        private readonly int _decodedRowBytes;
        // Encoded row bytes (PNG adds 1 filter byte; TIFF same as decoded).
        private readonly int _encodedRowBytes;

        // Row buffer. TIFF layout: [row data]; PNG layout: [margin bytes][filter byte][row data].
        private readonly byte[] _currentRow;
        // Previous row buffer for PNG predictors (same layout as _currentRow). Null for TIFF / identity.
        private readonly byte[] _previousRow;

        // Left margin size (bytesPerPixel) used only for PNG to eliminate left boundary checks; 0 otherwise.
        private readonly int _rowMarginBytes;
        // Index inside buffers where actual row pixel data begins (after margin and filter byte for PNG).
        private readonly int _rowDataOffset;

        // Current read offset inside logical decoded row (excluding margin/filter).
        private int _rowOffset;
        // End-of-stream flag for underlying source.
        private bool _endOfStream;
        // Whether _currentRow presently holds a decoded row ready for reading.
        private bool _currentRowValid;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

        /// <summary>
        /// Initializes a predictor decode stream that performs TIFF (2) or PNG (10..15) predictor undo on demand.
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
                _bytesPerSample = bitsPerComponent >= 8 ? (bitsPerComponent + 7) / 8 : 1;
                _decodedRowBytes = bitsPerComponent >= 8 ? columns * colors * _bytesPerSample : (columns * colors * bitsPerComponent + 7) / 8;
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
            _bytesPerSample = bitsPerComponent >= 8 ? (bitsPerComponent + 7) / 8 : 1;
            _decodedRowBytes = bitsPerComponent >= 8 ? columns * colors * _bytesPerSample : (columns * colors * bitsPerComponent + 7) / 8;
            _encodedRowBytes = predictor >= 10 ? _decodedRowBytes + 1 : _decodedRowBytes;

            if (predictor >= 10)
            {
                // PNG: buffer layout [margin][filter][data]. Margin length = bytesPerPixel.
                int bytesPerPixel = (_colors * _bitsPerComponent + 7) / 8;
                _rowMarginBytes = bytesPerPixel;
                _rowDataOffset = _rowMarginBytes + 1; // skip margin and filter byte
                int total = _rowMarginBytes + 1 + _decodedRowBytes;
                _currentRow = new byte[total];
                _previousRow = new byte[total];
                // Margin bytes implicitly zero. Filter byte will be written at index _rowMarginBytes.
            }
            else
            {
                // TIFF predictor: no margin, no filter byte.
                _rowMarginBytes = 0;
                _rowDataOffset = 0;
                _currentRow = new byte[_decodedRowBytes];
                _previousRow = null;
            }

            _rowOffset = 0;
            _endOfStream = false;
            _currentRowValid = false;
        }

        public override void Flush() { }

        /// <summary>
        /// Reads decoded predictor-undo row bytes into caller buffer.
        /// </summary>
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
                int toCopy = remainingInRow < count ? remainingInRow : count;
                Array.Copy(_currentRow, _rowDataOffset + _rowOffset, buffer, offset, toCopy);
                _rowOffset += toCopy;
                offset += toCopy;
                count -= toCopy;
                totalCopied += toCopy;
            }
            return totalCopied;
        }

        /// <summary>
        /// Decodes next encoded row from the source and applies predictor undo.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DecodeNextRow()
        {
            _rowOffset = 0;
            byte filterByte = 0;

            if (_predictor >= 10)
            {
                // PNG: read filter byte + row data directly into buffer starting at margin index.
                int start = _rowMarginBytes; // position of filter byte
                int needed = _encodedRowBytes; // filter + row data
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
                // TIFF or identity: just read row data.
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
                PngFilterUndo.UndoPngFilter(filterByte, _currentRow, _previousRow, _rowMarginBytes, _rowDataOffset, _decodedRowBytes);
                // Copy decoded pixel data (exclude margin + filter byte) for next row reference.
                Buffer.BlockCopy(_currentRow, _rowDataOffset, _previousRow, _rowDataOffset, _decodedRowBytes);
            }

            _currentRowValid = true;
            return true;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
