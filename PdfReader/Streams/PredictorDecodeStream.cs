using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PdfReader.Streams
{
    /// <summary>
    /// Streaming predictor undo wrapper for TIFF (2) and PNG (10..15) predictors.
    /// Does NOT pre-buffer the entire decoded data. Decodes one row at a time on demand.
    /// Supports bits per component: 1,2,4,8,16. For sub‑byte sample sizes packing is preserved.
    /// </summary>
    internal sealed class PredictorDecodeStream : Stream
    {
        private readonly Stream _source; // underlying decoded (filter chain already applied) stream
        private readonly int _predictor; // 2 (TIFF) or 10..15 (PNG filters)
        private readonly int _colors; // number of color components per pixel
        private readonly int _bitsPerComponent; // sample size
        private readonly int _columns; // pixel columns per row
        private readonly bool _leaveOpen;

        private readonly int _bytesPerSample; // 1 for <8 bpc, 1 for 8, 2 for 16
        private readonly int _decodedRowBytes; // bytes in decoded (post predictor) row
        private readonly int _encodedRowBytes; // bytes in encoded row (PNG only: +1 filter byte)
        private readonly byte[] _currentRow; // holds decoded row bytes
        private readonly byte[] _previousRow; // PNG filter reference; null for first row or TIFF predictor

        private int _rowOffset; // position inside current decoded row
        private bool _endOfStream; // reached end of source
        private bool _currentRowValid; // whether _currentRow contains decoded data

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

            if (predictor != 2 && (predictor < 10 || predictor > 15))
            {
                // Unsupported predictor: just use passthrough stream
                _source = decoded;
                _predictor = 1; // treat as identity
                _colors = colors;
                _bitsPerComponent = bitsPerComponent;
                _columns = columns;
                _bytesPerSample = bitsPerComponent >= 8 ? (bitsPerComponent + 7) / 8 : 1;
                _decodedRowBytes = bitsPerComponent >= 8 ? columns * colors * _bytesPerSample : (columns * colors * bitsPerComponent + 7) / 8;
                _encodedRowBytes = _decodedRowBytes;
                _currentRow = Array.Empty<byte>();
                _previousRow = null;
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
            _currentRow = new byte[_decodedRowBytes];
            _previousRow = predictor >= 10 ? new byte[_decodedRowBytes] : null;
            _rowOffset = 0;
            _endOfStream = false;
            _currentRowValid = false;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush() { }

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
                        break; // no more data
                    }
                    if (!DecodeNextRow())
                    {
                        break; // end of stream or decode failure
                    }
                }

                int remainingInRow = _decodedRowBytes - _rowOffset;
                int toCopy = remainingInRow < count ? remainingInRow : count;
                Array.Copy(_currentRow, _rowOffset, buffer, offset, toCopy);
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
            int readBytesNeeded = _encodedRowBytes;
            int readOffset = 0;
            byte filterByte = 0;
            if (_predictor >= 10)
            {
                // PNG: first byte is filter, then row data.
                int fb = _source.ReadByte();
                if (fb < 0)
                {
                    _endOfStream = true;
                    _currentRowValid = false;
                    return false;
                }
                filterByte = (byte)fb;
                readBytesNeeded = _decodedRowBytes;
            }
            while (readOffset < readBytesNeeded)
            {
                int read = _source.Read(_currentRow, readOffset, readBytesNeeded - readOffset);
                if (read <= 0)
                {
                    _endOfStream = true;
                    if (readOffset == 0)
                    {
                        _currentRowValid = false;
                        return false;
                    }
                    // Partial row; treat as end-of-stream, but still expose decoded partial bytes (unlikely). No predictor applied.
                    _currentRowValid = true;
                    return true;
                }
                readOffset += read;
            }

            if (_predictor == 2)
            {
                UndoTiffPredictor(_currentRow);
            }
            else if (_predictor >= 10 && _predictor <= 15)
            {
                UndoPngFilter(filterByte, _currentRow, _previousRow);
                // Remember current row for next filter pass.
                Array.Copy(_currentRow, 0, _previousRow, 0, _decodedRowBytes);
            }

            _currentRowValid = true;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UndoTiffPredictor(byte[] row)
        {
            // Left differencing per sample (modulo sample domain) applied in-place.
            int samplesPerRow = _columns * _colors;
            if (_bitsPerComponent >= 8)
            {
                if (_bytesPerSample == 1)
                {
                    for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                    {
                        int leftIndex = sampleIndex - _colors;
                        int left = leftIndex >= 0 ? row[leftIndex] : 0;
                        int current = row[sampleIndex];
                        row[sampleIndex] = (byte)((current + left) & 0xFF);
                    }
                }
                else // 16 bpc
                {
                    for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                    {
                        int byteIndex = sampleIndex * 2;
                        int current = (row[byteIndex] << 8) | row[byteIndex + 1];
                        int left = 0;
                        if (sampleIndex >= _colors)
                        {
                            int leftByteIndex = (sampleIndex - _colors) * 2;
                            left = (row[leftByteIndex] << 8) | row[leftByteIndex + 1];
                        }
                        int decoded = (current + left) & 0xFFFF;
                        row[byteIndex] = (byte)(decoded >> 8);
                        row[byteIndex + 1] = (byte)(decoded & 0xFF);
                    }
                }
            }
            else
            {
                // Sub-byte path: expand into temporary sample array then repack.
                int bits = _bitsPerComponent;
                int sampleMask = (1 << bits) - 1;
                int[] samples = new int[samplesPerRow];
                int bitPos = 0;
                for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                {
                    int byteIndex = bitPos >> 3;
                    int intraBits = bitPos & 7;
                    int remainingBits = 8 - intraBits;
                    int value;
                    if (remainingBits >= bits)
                    {
                        int shift = remainingBits - bits;
                        value = (row[byteIndex] >> shift) & sampleMask;
                    }
                    else
                    {
                        int firstPart = row[byteIndex] & ((1 << remainingBits) - 1);
                        int secondPart = row[byteIndex + 1] >> (8 - (bits - remainingBits));
                        value = ((firstPart << (bits - remainingBits)) | secondPart) & sampleMask;
                    }
                    int leftIndex = sampleIndex - _colors;
                    int left = leftIndex >= 0 ? samples[leftIndex] : 0;
                    samples[sampleIndex] = (value + left) & sampleMask;
                    bitPos += bits;
                }
                // Repack.
                Array.Clear(row, 0, row.Length);
                int outBitPos = 0;
                for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                {
                    int value = samples[sampleIndex] & sampleMask;
                    int outByteIndex = outBitPos >> 3;
                    int outIntra = outBitPos & 7;
                    int freeBits = 8 - outIntra;
                    if (freeBits >= bits)
                    {
                        int shift = freeBits - bits;
                        row[outByteIndex] &= (byte)~(((sampleMask) << shift) & 0xFF);
                        row[outByteIndex] |= (byte)((value & sampleMask) << shift);
                    }
                    else
                    {
                        int firstBits = freeBits;
                        int secondBits = bits - firstBits;
                        int firstMask = (1 << firstBits) - 1;
                        int firstValue = (value >> secondBits) & firstMask;
                        row[outByteIndex] &= (byte)~firstMask;
                        row[outByteIndex] |= (byte)firstValue;
                        int secondValue = value & ((1 << secondBits) - 1);
                        int secondShift = 8 - secondBits;
                        row[outByteIndex + 1] &= (byte)~(((1 << secondBits) - 1) << secondShift);
                        row[outByteIndex + 1] |= (byte)(secondValue << secondShift);
                    }
                    outBitPos += bits;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UndoPngFilter(byte filter, byte[] currentRow, byte[] previousRow)
        {
            int bytesPerPixel = (_colors * _bitsPerComponent + 7) / 8; // PNG definition
            for (int i = 0; i < _decodedRowBytes; i++)
            {
                int raw = currentRow[i];
                int left = i >= bytesPerPixel ? currentRow[i - bytesPerPixel] : 0;
                int up = previousRow != null ? previousRow[i] : 0;
                int upLeft = (previousRow != null && i >= bytesPerPixel) ? previousRow[i - bytesPerPixel] : 0;
                int decoded = filter switch
                {
                    0 => raw,
                    1 => raw + left,
                    2 => raw + up,
                    3 => raw + ((left + up) >> 1),
                    4 => raw + Paeth(left, up, upLeft),
                    _ => raw
                };
                currentRow[i] = (byte)decoded;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            if (pb <= pc)
            {
                return b;
            }
            return c;
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
