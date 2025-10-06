using System;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace PdfReader.Rendering.Image.Raw
{
    /// <summary>
    /// Stateful row decoder for raw (non-JPEG) PDF images that only need predictor undo + row extraction.
    /// Produces per-row, post-predictor, component-interleaved sample data in the original bit depth layout
    /// (packed for 1/2/4 bpc, byte per sample for 8 bpc, big-endian 2 bytes per sample for 16 bpc).
    /// The consumer supplies an adequate destination buffer each call. Lifetime: construct once, call
    /// <see cref="DecodeNexRow"/> until it returns false, then dispose.
    /// Unsupported predictor combinations (e.g. TIFF predictor with sub-8 bpc) throw a <see cref="NotSupportedException"/>
    /// instead of allocating a fallback full buffer.
    /// </summary>
    internal sealed unsafe class PdfRawImageRowDecoder : IDisposable
    {
        private readonly PdfImage _image;
        private readonly ReadOnlyMemory<byte> _rawData;
        private readonly MemoryHandle _pinned;
        private readonly byte* _basePtr;

        private readonly int _width;
        private readonly int _height;
        private readonly int _components;
        private readonly int _bitsPerComponent;

        private readonly int _predictor;          // 1 = none, 2 = TIFF, 10..15 = PNG filters
        private readonly int _predictorColors;
        private readonly int _predictorBits;
        private readonly int _predictorColumns;

        private readonly int _decodedRowBytes;    // decoded (post-predictor) row length in bytes
        private readonly int _encodedRowBytes;    // encoded row length (adds 1 filter byte for PNG predictors)

        private int _currentRowIndex;

        private readonly byte[] _prevPngRow;      // prior decoded PNG row (for filters using 'up')
        private readonly byte[] _workRow;         // work buffer for TIFF / PNG predictor undo

        private readonly bool _streamingSupported;

        private bool _disposed;

        /// <summary>
        /// Row length in bytes that will be written to caller's destination buffer per successful decode call.
        /// </summary>
        public int DecodedRowByteLength
        {
            get { return _decodedRowBytes; }
        }

        /// <summary>
        /// Create a row decoder over raw (post-filter) image data. Throws if unsupported predictor combination.
        /// </summary>
        public PdfRawImageRowDecoder(PdfImage image, ReadOnlyMemory<byte> rawData)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }
            if (rawData.Length == 0)
            {
                throw new ArgumentException("Raw data buffer is empty.", nameof(rawData));
            }

            _image = image;
            _rawData = rawData;
            _width = image.Width;
            _height = image.Height;
            if (_width <= 0 || _height <= 0)
            {
                throw new ArgumentException("Invalid image dimensions.");
            }

            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                throw new InvalidOperationException("Missing color space converter.");
            }
            _components = converter.Components;
            _bitsPerComponent = image.BitsPerComponent;
            if (_components <= 0)
            {
                throw new InvalidOperationException("Invalid component count.");
            }

            _predictor = 1;
            _predictorColors = _components;
            _predictorBits = _bitsPerComponent;
            _predictorColumns = _width;
            if (image.DecodeParms != null && image.DecodeParms.Count > 0 && image.DecodeParms[0] != null)
            {
                var parms = image.DecodeParms[0];
                _predictor = parms.Predictor ?? 1;
                _predictorColors = parms.Colors ?? _components;
                _predictorBits = parms.BitsPerComponent ?? _bitsPerComponent;
                _predictorColumns = parms.Columns ?? _width;
            }

            if (_predictorBits >= 8)
            {
                int bytesPerSample = (_predictorBits + 7) / 8; // 1 or 2
                _decodedRowBytes = _predictorColumns * _predictorColors * bytesPerSample;
            }
            else
            {
                _decodedRowBytes = (_predictorColumns * _predictorColors * _predictorBits + 7) / 8;
            }

            if (_predictor >= 10 && _predictor <= 15)
            {
                _encodedRowBytes = _decodedRowBytes + 1; // filter byte
            }
            else
            {
                _encodedRowBytes = _decodedRowBytes; // no filter byte
            }

            _streamingSupported = _predictor == 1
                                   || (_predictor == 2 && _predictorBits >= 8)
                                   || (_predictor >= 10 && _predictor <= 15);

            if (!_streamingSupported)
            {
                // Explicitly disallow unsupported streaming combinations (e.g., TIFF predictor with sub‑8 bpc)
                throw new NotSupportedException("Streaming row decode not supported for this predictor / bit depth combination.");
            }

            if (_predictor == 2 && _predictorBits >= 8)
            {
                _workRow = new byte[_decodedRowBytes];
            }
            else if (_predictor >= 10 && _predictor <= 15)
            {
                _workRow = new byte[_decodedRowBytes];
                _prevPngRow = new byte[_decodedRowBytes];
            }
            // predictor == 1: direct slicing, no work row

            _pinned = rawData.Pin();
            _basePtr = (byte*)_pinned.Pointer;
            _currentRowIndex = 0;
        }

        /// <summary>
        /// Decode the next row into the provided destination buffer. Destination size must be at least <see cref="DecodedRowByteLength"/>.
        /// Returns false when all rows have been produced.
        /// </summary>
        /// <param name="destination">Caller allocated destination buffer pointer.</param>
        public bool DecodeNexRow(byte* destination)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PdfRawImageRowDecoder));
            }
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            if (_currentRowIndex >= _height)
            {
                return false;
            }

            switch (_predictor)
            {
                case 1:
                {
                    byte* src = _basePtr + _currentRowIndex * _encodedRowBytes;
                    Buffer.MemoryCopy(src, destination, _decodedRowBytes, _decodedRowBytes);
                    break;
                }
                case 2:
                {
                    DecodeTiffRow(destination);
                    break;
                }
                default:
                {
                    DecodePngRow(destination);
                    break;
                }
            }

            _currentRowIndex++;
            return true;
        }

        /// <summary>
        /// Convenience correctly spelled alias.
        /// </summary>
        public bool DecodeNextRow(byte* destination)
        {
            return DecodeNexRow(destination);
        }

        private void DecodeTiffRow(byte* destination)
        {
            byte* encoded = _basePtr + _currentRowIndex * _encodedRowBytes;
            fixed (byte* workPtr = _workRow)
            {
                Buffer.MemoryCopy(encoded, workPtr, _decodedRowBytes, _decodedRowBytes);
                int bytesPerSample = (_predictorBits + 7) / 8; // 1 or 2
                int totalSamples = _predictorColumns * _predictorColors;
                if (bytesPerSample == 1)
                {
                    for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
                    {
                        int leftIndex = sampleIndex - _predictorColors;
                        int left = leftIndex >= 0 ? workPtr[leftIndex] : 0;
                        workPtr[sampleIndex] = (byte)((workPtr[sampleIndex] + left) & 0xFF);
                    }
                }
                else
                {
                    for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
                    {
                        int byteIndex = sampleIndex * 2;
                        int current = (workPtr[byteIndex] << 8) | workPtr[byteIndex + 1];
                        int left = 0;
                        if (sampleIndex >= _predictorColors)
                        {
                            int leftByteIndex = (sampleIndex - _predictorColors) * 2;
                            left = (workPtr[leftByteIndex] << 8) | workPtr[leftByteIndex + 1];
                        }
                        int decoded = (current + left) & 0xFFFF;
                        workPtr[byteIndex] = (byte)(decoded >> 8);
                        workPtr[byteIndex + 1] = (byte)(decoded & 0xFF);
                    }
                }
                Buffer.MemoryCopy(workPtr, destination, _decodedRowBytes, _decodedRowBytes);
            }
        }

        private void DecodePngRow(byte* destination)
        {
            int encodedStride = _encodedRowBytes;
            byte* encodedRow = _basePtr + _currentRowIndex * encodedStride;
            byte filter = encodedRow[0];
            fixed (byte* workPtr = _workRow)
            fixed (byte* prevPtr = _prevPngRow)
            {
                int bytesPerPixel = (_predictorColors * _predictorBits + 7) / 8;
                for (int i = 0; i < _decodedRowBytes; i++)
                {
                    int raw = encodedRow[1 + i];
                    int left = i >= bytesPerPixel ? workPtr[i - bytesPerPixel] : 0;
                    int up = _currentRowIndex > 0 ? prevPtr[i] : 0;
                    int upLeft = (_currentRowIndex > 0 && i >= bytesPerPixel) ? prevPtr[i - bytesPerPixel] : 0;
                    int decoded = filter switch
                    {
                        0 => raw,
                        1 => raw + left,
                        2 => raw + up,
                        3 => raw + ((left + up) >> 1),
                        4 => raw + PaethPredictor(left, up, upLeft),
                        _ => raw
                    };
                    workPtr[i] = (byte)decoded;
                }
                Buffer.MemoryCopy(workPtr, destination, _decodedRowBytes, _decodedRowBytes);
                Buffer.MemoryCopy(workPtr, prevPtr, _decodedRowBytes, _decodedRowBytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PaethPredictor(int a, int b, int c)
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _pinned.Dispose();
            _disposed = true;
        }
    }
}
