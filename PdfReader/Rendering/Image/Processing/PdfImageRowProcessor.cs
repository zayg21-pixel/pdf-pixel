using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Row-oriented image post processor that converts already decoded sample rows into final output buffers.
    /// All processing occurs in an 8-bit pipeline; 16-bit input components are downscaled (rounded) to 8-bit.
    /// </summary>
    internal sealed class PdfImageRowProcessor : IDisposable
    {
        private enum OutputMode
        {
            Alpha,
            Gray,
            IndexedRgba,
            Rgba
        }

        private readonly PdfImage _image;
        private readonly PdfColorSpaceConverter _converter;
        private readonly ILogger _logger;

        private readonly int _width;
        private readonly int _height;
        private readonly int _bitsPerComponent;
        private readonly int _components; // validated: 1,3,4

        private readonly IndexedConverter _indexedConverter;
        private readonly SKColor[] _indexedPalette;
        private readonly int[] _maskArray; // normalized [min,max] ordered pairs per component or null

        private readonly byte[] _decodeArray; // [minByte, maxByte] pairs per component, null when identity

        private readonly OutputMode _outputMode;

        private IntPtr _buffer;
        private int _rowStride;
        private bool _initialized;
        private bool _completed;

        public PdfImageRowProcessor(PdfImage image, ILogger logger)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            // Normalize /Mask color key pairs via utility (ensures ascending ordering per component).
            _maskArray = ProcessingUtilities.BuildNormalizedMaskRawPairs(_components, _image.MaskArray);

            var sourceDecode = image.DecodeArray; // local reference (not stored)
            _decodeArray = ProcessingUtilities.BuildDecodeMinSpanBytes(_components, sourceDecode, image.HasImageMask);

            bool alphaOnly = image.HasImageMask || image.IsSoftMask;
            if (alphaOnly)
            {
                _outputMode = OutputMode.Alpha;
            }
            else if (_converter is IndexedConverter indexed)
            {
                _indexedConverter = indexed;
                _indexedPalette = indexed.BuildPalette(image.RenderingIntent);

                // Indexed /Decode ignored; warn if source decode differs from default raw domain identity.
                if (sourceDecode != null && sourceDecode.Length == _components * 2)
                {
                    int rawMax = _bitsPerComponent == 16 ? 255 : ((1 << _bitsPerComponent) - 1);
                    float dMin = sourceDecode[0];
                    float dMax = sourceDecode[1];
                    bool isDefault = Math.Abs(dMin - 0f) < 1e-12f && Math.Abs(dMax - rawMax) < 1e-9f;
                    if (!isDefault)
                    {
                        _logger.LogWarning("Indexed image /Decode array ignored (Name={Name}) Range=[{Min} {Max}] RawMax={RawMax}", _image.Name, dMin, dMax, rawMax);
                    }
                }

                _outputMode = OutputMode.IndexedRgba;
            }
            else
            {
                bool canGray = _components == 1 && _converter.IsDevice && _maskArray == null;
                _outputMode = canGray ? OutputMode.Gray : OutputMode.Rgba;
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

            if (_outputMode == OutputMode.Alpha || _outputMode == OutputMode.Gray)
            {
                _rowStride = _width; // Alpha8 or Gray8
            }
            else
            {
                _rowStride = _width * 4; // RGBA
            }

            long totalBytes = (long)_rowStride * _height;
            _buffer = Marshal.AllocHGlobal(new IntPtr(totalBytes));
            _initialized = true;
        }

        /// <summary>
        /// Writes a fully decoded (post-filter, predictor undone) component-interleaved sample row into the output buffer.
        /// The row pointer must reference exactly the bytes for a single row of component samples in the encoded layout.
        /// For bits per component < 8 samples are tightly packed; 16 bpc is downscaled.
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

            switch (_outputMode)
            {
                case OutputMode.Alpha:
                case OutputMode.Gray:
                {
                    ProcessSingleChannelRow(decodedRow, destRow);
                    break;
                }
                case OutputMode.IndexedRgba:
                {
                    ProcessIndexedRgbaRow(decodedRow, destRow);
                    break;
                }
                default:
                {
                    ProcessRgbaRow(decodedRow, destRow);
                    break;
                }
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
            switch (_outputMode)
            {
                case OutputMode.Alpha:
                {
                    colorType = SKColorType.Alpha8;
                    alphaType = SKAlphaType.Unpremul;
                    break;
                }
                case OutputMode.Gray:
                {
                    colorType = SKColorType.Gray8;
                    alphaType = SKAlphaType.Opaque;
                    break;
                }
                default:
                {
                    colorType = SKColorType.Rgba8888;
                    alphaType = _outputMode == OutputMode.IndexedRgba ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
                    break;
                }
            }

            SKImageInfo info = new SKImageInfo(_width, _height, colorType, alphaType);
            SKPixmap pixmap = new SKPixmap(info, _buffer, _rowStride);
            SKImageRasterReleaseDelegate release = (addr, ctx) => Marshal.FreeHGlobal(addr);
            SKImage image = SKImage.FromPixels(pixmap, release);
            if (image == null)
            {
                throw new InvalidOperationException("Failed to create SKImage from row processor buffer.");
            }
            _completed = true;
            return image;
        }

        private unsafe void ProcessSingleChannelRow(byte* sourceRow, byte* destRow)
        {
            // Gray path: no color key masking.
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int sampleIndex = columnIndex * _components;
                int rawCode = ReadRawSample(sourceRow, sampleIndex);
                int expanded = ExpandRawSample(rawCode);
                byte grayValue = DecodeExpandedSample(expanded, 0);
                destRow[columnIndex] = grayValue;
            }
        }

        private unsafe void ProcessRgbaRow(byte* sourceRow, byte* destRow)
        {
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                int pixelBaseSampleIndex = columnIndex * _components;
                bool shouldMask = _components != 4 && _maskArray != null;
                for (int componentIndex = 0; componentIndex < _components; componentIndex++)
                {
                    int sampleIndex = pixelBaseSampleIndex + componentIndex;
                    int rawCode = ReadRawSample(sourceRow, sampleIndex);
                    UpdateMaskFlag(rawCode, componentIndex, ref shouldMask); // Only RGBA path applies color key masking.
                    int expanded = ExpandRawSample(rawCode);
                    byte decoded = DecodeExpandedSample(expanded, componentIndex);
                    destRow[rgbaBase + componentIndex] = decoded;
                }

                if (_components != 4)
                {
                    destRow[rgbaBase + 3] = shouldMask ? (byte)0 : (byte)255;
                }
            }
            _converter.Sample8RgbaInPlace(destRow, _width, _image.RenderingIntent);
        }

        private unsafe void ProcessIndexedRgbaRow(byte* sourceRow, byte* destRow)
        {
            int hiVal = _indexedPalette.Length - 1;
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                int sampleIndex = columnIndex * _components;
                int rawIndex = ReadRawSample(sourceRow, sampleIndex);
                if (rawIndex > hiVal)
                {
                    rawIndex = hiVal;
                }
                SKColor color = _indexedPalette[rawIndex];
                destRow[rgbaBase] = color.Red;
                destRow[rgbaBase + 1] = color.Green;
                destRow[rgbaBase + 2] = color.Blue;
                destRow[rgbaBase + 3] = 255;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe int ReadRawSample(byte* rowPtr, int sampleIndex)
        {
            switch (_bitsPerComponent)
            {
                case 16:
                {
                    int byteIndex = sampleIndex * 2;
                    int hi = rowPtr[byteIndex];
                    int lo = rowPtr[byteIndex + 1];
                    return (hi << 8) | lo; // 0..65535
                }
                case 8:
                {
                    return rowPtr[sampleIndex]; // 0..255
                }
                case 4:
                {
                    int byteIndex = sampleIndex >> 1;
                    bool highNibble = (sampleIndex & 1) == 0;
                    int value = rowPtr[byteIndex];
                    return highNibble ? (value >> 4) & 0x0F : value & 0x0F; // 0..15
                }
                case 2:
                {
                    int byteIndex = sampleIndex >> 2;
                    int shift = 6 - ((sampleIndex & 3) * 2);
                    return (rowPtr[byteIndex] >> shift) & 0x03; // 0..3
                }
                case 1:
                {
                    int byteIndex = sampleIndex >> 3;
                    int shift = 7 - (sampleIndex & 7);
                    return (rowPtr[byteIndex] >> shift) & 0x01; // 0..1
                }
                default:
                {
                    return 0;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ExpandRawSample(int rawCode)
        {
            int expanded;
            switch (_bitsPerComponent)
            {
                case 16:
                {
                    expanded = (rawCode + 128) >> 8; // rounded high-byte
                    break;
                }
                case 8:
                {
                    expanded = rawCode;
                    break;
                }
                case 4:
                {
                    expanded = (rawCode & 0x0F) * 17;
                    break;
                }
                case 2:
                {
                    expanded = (rawCode & 0x03) * 85;
                    break;
                }
                case 1:
                {
                    expanded = (rawCode & 0x01) * 255;
                    break;
                }
                default:
                {
                    expanded = 0;
                    break;
                }
            }
            return expanded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte DecodeExpandedSample(int expanded, int componentIndex)
        {
            if (_decodeArray == null)
            {
                return (byte)expanded;
            }
            int decodePairIndex = componentIndex * 2;
            int minByte = _decodeArray[decodePairIndex];
            int maxByte = _decodeArray[decodePairIndex + 1];
            if (minByte == maxByte)
            {
                return (byte)minByte;
            }
            int span = maxByte - minByte;
            int mappedValue;
            if (span > 0)
            {
                mappedValue = minByte + (expanded * span + 127) / 255;
            }
            else
            {
                span = -span;
                mappedValue = minByte - (expanded * span + 127) / 255;
            }
            if (mappedValue < 0)
            {
                mappedValue = 0;
            }
            else if (mappedValue > 255)
            {
                mappedValue = 255;
            }
            return (byte)mappedValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMaskFlag(int rawCode, int componentIndex, ref bool shouldMask)
        {
            if (!shouldMask)
            {
                return;
            }
            if (_maskArray == null)
            {
                return;
            }
            int pairIndex = componentIndex * 2;
            int minRaw = _maskArray[pairIndex];
            int maxRaw = _maskArray[pairIndex + 1];
            if (rawCode < minRaw || rawCode > maxRaw)
            {
                shouldMask = false;
            }
        }

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
