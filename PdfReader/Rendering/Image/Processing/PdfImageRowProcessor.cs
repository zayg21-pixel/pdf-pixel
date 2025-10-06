using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Row-oriented image post processor that converts already decoded (filter chain and predictor undone)
    /// sample rows into final output buffers (Gray8, Alpha8 or RGBA8888) without allocating a full
    /// intermediate component image. One instance is intended per <see cref="PdfImage"/>.
    /// All processing is performed in an 8-bit pipeline; higher precision (16 bpc) input is downscaled
    /// to 8-bit (rounded) in <see cref="ReadSample"/>.
    /// </summary>
    internal sealed class PdfImageRowProcessor : IDisposable
    {
        private readonly PdfImage _image;
        private readonly PdfColorSpaceConverter _converter;
        private readonly ILogger _logger;

        private readonly int _width;
        private readonly int _height;
        private readonly int _bitsPerComponent;
        private readonly int _components; // validated: 1, 3 or 4

        private readonly bool _hasColorKeyMask;
        private readonly int[] _minInclusive; // scaled 0..255 domain
        private readonly int[] _maxInclusive; // scaled 0..255 domain

        private readonly bool _isIndexed;
        private readonly IndexedConverter _indexedConverter;
        private readonly SKColor[] _indexedPalette;
        private readonly int[] _indexedDecodeMap;

        private readonly byte[][] _decodeLuts; // per-component 8-bit decode LUTs; null if identity

        private IntPtr _buffer;
        private int _rowStride;
        private bool _initialized;
        private bool _completed;

        private readonly bool _outputAlphaOnly;
        private readonly bool _outputGray;
        private readonly bool _outputRgba;

        /// <summary>
        /// Create a row processor for the specified image. Assumes predictor already undone.
        /// </summary>
        public PdfImageRowProcessor(PdfImage image, ILogger logger)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            _image = image;
            _logger = logger;

            _width = image.Width;
            _height = image.Height;
            _bitsPerComponent = image.BitsPerComponent;
            _converter = image.ColorSpaceConverter ?? throw new InvalidOperationException("Color space converter must not be null for row processing.");

            if (_width <= 0 || _height <= 0)
            {
                throw new ArgumentException("Image dimensions must be positive.");
            }

            if (_bitsPerComponent != 1 && _bitsPerComponent != 2 && _bitsPerComponent != 4 && _bitsPerComponent != 8 && _bitsPerComponent != 16)
            {
                throw new NotSupportedException("Row processor supports 1,2,4,8,16 bits per component only.");
            }

            _components = _converter.Components;
            if (_components != 1 && _components != 3 && _components != 4)
            {
                throw new NotSupportedException("Unsupported component count. Expected 1, 3 or 4.");
            }

            _hasColorKeyMask = ProcessingUtilities.TryBuildColorKeyRanges(
                _components,
                _bitsPerComponent,
                image.MaskArray,
                out _minInclusive,
                out _maxInclusive);

            if (_converter is IndexedConverter idx)
            {
                _isIndexed = true;
                _indexedConverter = idx;
                _indexedPalette = idx.BuildPalette(image.RenderingIntent);
                _indexedDecodeMap = ProcessingUtilities.BuildIndexedDecodeMap(_indexedPalette.Length, _bitsPerComponent, image.DecodeArray);
            }

            if (ProcessingUtilities.ApplyDecode(image.DecodeArray))
            {
                _decodeLuts = ProcessingUtilities.Build8BitDecodeLuts(_components, _bitsPerComponent, image.DecodeArray);
            }

            _outputAlphaOnly = image.HasImageMask || image.IsSoftMask;
            if (_outputAlphaOnly)
            {
                _outputGray = false;
                _outputRgba = false;
            }
            else
            {
                bool singleComponentDeviceGray = _components == 1 && _converter.IsDevice && !_isIndexed;
                bool canGray = singleComponentDeviceGray && !_hasColorKeyMask;
                _outputGray = canGray;
                _outputRgba = !_outputGray;
            }
        }

        /// <summary>
        /// Gets the row stride in bytes for the output buffer (set after initialization).
        /// </summary>
        public int RowStride
        {
            get
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException("Buffer not initialized.");
                }
                return _rowStride;
            }
        }

        /// <summary>
        /// Allocates the destination buffer based on the selected output mode.
        /// Must be called before any <see cref="WriteRow"/> invocation.
        /// </summary>
        public void InitializeBuffer()
        {
            if (_initialized)
            {
                return;
            }

            if (_outputAlphaOnly || _outputGray)
            {
                _rowStride = _width; // Alpha8 or Gray8 output
            }
            else
            {
                _rowStride = _width * 4; // RGBA output
            }

            long totalBytes = (long)_rowStride * _height;
            _buffer = Marshal.AllocHGlobal(new IntPtr(totalBytes));
            _initialized = true;
        }

        /// <summary>
        /// Writes a fully decoded (post-filter, predictor undone) component-interleaved sample row into the output buffer.
        /// The row pointer must reference exactly the bytes for a single row of component samples in the encoded layout.
        /// For bits per component &lt; 8 samples are tightly packed; 16 bpc is downscaled.
        /// </summary>
        public unsafe void WriteRow(int rowIndex, byte* decodedRow)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("InitializeBuffer must be called before WriteRow.");
            }
            if (decodedRow == null)
            {
                throw new ArgumentNullException(nameof(decodedRow));
            }
            if (rowIndex < 0 || rowIndex >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
            if (_completed)
            {
                throw new InvalidOperationException("Image already completed.");
            }

            byte* destRow = (byte*)_buffer + rowIndex * _rowStride;

            if (_outputAlphaOnly)
            {
                ProcessAlphaOnlyRow(decodedRow, destRow);
            }
            else if (_outputGray)
            {
                ProcessGrayRow(decodedRow, destRow);
            }
            else if (_isIndexed)
            {
                ProcessIndexedRgbaRow(decodedRow, destRow);
            }
            else
            {
                ProcessRgbaRow(decodedRow, destRow);
            }
        }

        /// <summary>
        /// Returns an SKImage wrapping the unmanaged buffer. Ownership of buffer transfers to Skia.
        /// </summary>
        public SKImage GetSkImage()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("InitializeBuffer must be called before GetSkImage.");
            }
            if (_completed)
            {
                throw new InvalidOperationException("GetSkImage already called.");
            }

            SKColorType colorType;
            SKAlphaType alphaType;
            if (_outputAlphaOnly)
            {
                colorType = SKColorType.Alpha8;
                alphaType = SKAlphaType.Unpremul;
            }
            else if (_outputGray)
            {
                colorType = SKColorType.Gray8;
                alphaType = SKAlphaType.Opaque;
            }
            else
            {
                colorType = SKColorType.Rgba8888;
                alphaType = SKAlphaType.Unpremul;
            }

            var info = new SKImageInfo(_width, _height, colorType, alphaType);
            var pixmap = new SKPixmap(info, _buffer, _rowStride);
            SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
            SKImage image = SKImage.FromPixels(pixmap, release);
            if (image == null)
            {
                throw new InvalidOperationException("Failed to create SKImage from row processor buffer.");
            }
            _completed = true;
            return image;
        }

        private unsafe void ProcessAlphaOnlyRow(byte* sourceRow, byte* destRow)
        {
            // Image or soft mask: single component per sample. /Decode already captured via LUT if present.
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int sampleIndex = columnIndex * _components; // _components is 1 here but keep formula for clarity/consistency.
                byte raw = ReadSample(sourceRow, sampleIndex);
                if (_decodeLuts != null)
                {
                    destRow[columnIndex] = _decodeLuts[0][raw];
                }
                else
                {
                    destRow[columnIndex] = raw;
                }
            }
        }

        private unsafe void ProcessGrayRow(byte* sourceRow, byte* destRow)
        {
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int sampleIndex = columnIndex * _components; // Gray: components == 1
                byte raw = ReadSample(sourceRow, sampleIndex);
                byte value = raw;
                if (_decodeLuts != null)
                {
                    value = _decodeLuts[0][raw];
                }
                destRow[columnIndex] = value;
            }
        }

        private unsafe void ProcessRgbaRow(byte* sourceRow, byte* destRow)
        {
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                bool masked = _hasColorKeyMask;
                int pixelBaseSampleIndex = columnIndex * _components;
                for (int componentIndex = 0; componentIndex < _components; componentIndex++)
                {
                    int sampleIndex = pixelBaseSampleIndex + componentIndex;
                    byte raw = ReadSample(sourceRow, sampleIndex);
                    byte decoded = raw;
                    if (_decodeLuts != null)
                    {
                        decoded = _decodeLuts[componentIndex][raw];
                    }

                    if (_hasColorKeyMask && masked && componentIndex < _minInclusive.Length)
                    {
                        int min = _minInclusive[componentIndex];
                        int max = _maxInclusive[componentIndex];
                        if (decoded < min || decoded > max)
                        {
                            masked = false;
                        }
                    }

                    destRow[rgbaBase + componentIndex] = decoded;
                }

                if (_components != 4)
                {
                    destRow[rgbaBase + 3] = masked ? (byte)0 : (byte)255;
                }
            }

            _converter.Sample8RgbaInPlace(destRow, _width, _image.RenderingIntent);
        }

        private unsafe void ProcessIndexedRgbaRow(byte* sourceRow, byte* destRow)
        {
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                int sampleIndex = columnIndex * _components; // Indexed: components == 1 (index)
                byte raw = ReadSample(sourceRow, sampleIndex);
                int rawIndex = raw;
                if ((uint)rawIndex >= (uint)_indexedDecodeMap.Length)
                {
                    rawIndex = 0;
                }
                int paletteIndex = _indexedDecodeMap[rawIndex];
                SKColor color = (paletteIndex >= 0 && paletteIndex < _indexedPalette.Length) ? _indexedPalette[paletteIndex] : SKColors.White;
                destRow[rgbaBase] = color.Red;
                destRow[rgbaBase + 1] = color.Green;
                destRow[rgbaBase + 2] = color.Blue;
                destRow[rgbaBase + 3] = 255;
            }
        }

        /// <summary>
        /// Read a raw sample code as 8-bit value from a packed row pointer (no filter/predictor work here).
        /// Supports 1,2,4,8,16 bits per component. 16 bpc is downscaled using rounded high-byte extraction.
        ///
        /// Optimization note: The caller now precomputes sampleIndex (columnIndex * _components + componentIndex)
        /// to avoid redoing that multiplication inside this hot path. This method is inlined and only branches
        /// on the bits-per-component mode which is invariant for the image instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe byte ReadSample(byte* rowPtr, int sampleIndex)
        {
            switch (_bitsPerComponent)
            {
                case 16:
                {
                    int byteIndex = sampleIndex * 2;
                    int hi = rowPtr[byteIndex];
                    int lo = rowPtr[byteIndex + 1];
                    int value16 = (hi << 8) | lo; // 0..65535
                    int value8 = (value16 + 128) >> 8; // Rounded downscale
                    return (byte)value8;
                }
                case 8:
                {
                    return rowPtr[sampleIndex];
                }
                case 4:
                {
                    int byteIndex = sampleIndex >> 1;
                    bool highNibble = (sampleIndex & 1) == 0;
                    int value = rowPtr[byteIndex];
                    int nibble = highNibble ? (value >> 4) & 0x0F : value & 0x0F; // 0..15
                    return (byte)nibble;
                }
                case 2:
                {
                    int byteIndex = sampleIndex >> 2;
                    int shift = 6 - ((sampleIndex & 3) * 2);
                    int value = (rowPtr[byteIndex] >> shift) & 0x03; // 0..3
                    return (byte)value;
                }
                case 1:
                {
                    int byteIndex = sampleIndex >> 3;
                    int shift = 7 - (sampleIndex & 7);
                    int value = (rowPtr[byteIndex] >> shift) & 0x01; // 0..1
                    return (byte)value;
                }
                default:
                {
                    throw new NotSupportedException("Unsupported bits per component in row reader.");
                }
            }
        }

        /// <summary>
        /// Frees unmanaged buffer if still owned.
        /// </summary>
        public void Dispose()
        {
            if (!_completed && _buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }
        }
    }
}
