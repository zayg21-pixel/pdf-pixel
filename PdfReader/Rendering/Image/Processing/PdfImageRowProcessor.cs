using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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

            // Normalize and scale /Mask color key pairs via utility (ensures ascending ordering per component).
            _maskArray = ProcessingUtilities.BuildNormalizedMaskRawPairs(_components, _image.MaskArray, _bitsPerComponent);

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
                    PdfImageGrayUpsampler.UpsampleScaleGrayRow(decodedRow, destRow, _width, _bitsPerComponent);
                    break;
                }
                case OutputMode.Rgba:
                {
                    PdfImageRgbaUpsampler.UpsampleScaleRgbaRow(decodedRow, destRow, _width, _components, _bitsPerComponent);
                    break;
                }
                case OutputMode.IndexedRgba:
                {
                    ProcessIndexedRgbaRow(decodedRow, destRow);
                    break;
                }
            }
        }

        /// <summary>
        /// Returns an SKImage wrapping the unmanaged buffer. Ownership of buffer transfers to Skia.
        /// </summary>
        public unsafe SKImage GetSkImage()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("InitializeBuffer must be called before GetSkImage.");
            }
            if (_completed)
            {
                throw new InvalidOperationException("GetSkImage already called.");
            }

            // Apply /Decode mapping and color space conversion in parallel for non-indexed outputs.
            switch (_outputMode)
            {
                case OutputMode.Alpha:
                case OutputMode.Gray:
                    {
                        byte* basePtr = (byte*)_buffer;

                        if (_decodeArray != null)
                        {
                            Parallel.For(0, _height, rowIndex =>
                            {
                                byte* rowPtr = basePtr + rowIndex * _rowStride;
                                ApplyDecodeMappingSingleComponentRow(rowPtr);
                            });
                        }

                        break;
                    }
                case OutputMode.Rgba:
                    {
                        byte* basePtr = (byte*)_buffer;

                        if (_decodeArray != null || _maskArray != null || _converter is not DeviceRgbConverter)
                        {
                            Parallel.For(0, _height, rowIndex =>
                            {
                                byte* rowPtr = basePtr + rowIndex * _rowStride;

                                if (_maskArray != null && _components != 4)
                                {
                                    ApplyMaskToSingleRgbaRow(rowPtr);
                                }

                                if (_decodeArray != null)
                                {
                                    ApplyDecodeMappingRgbaRow(rowPtr);
                                }

                                if (_converter is not DeviceRgbConverter)
                                {
                                    _converter.Sample8RgbaInPlace(rowPtr, _width, _image.RenderingIntent);
                                }
                            });
                        }

                        break;
                    }
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

        private unsafe void ProcessIndexedRgbaRow(byte* sourceRow, byte* destRow)
        {
            // TODO: we might need to optimize this later
            int hiVal = _indexedPalette.Length - 1;
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int rgbaBase = columnIndex * 4;
                int sampleIndex = columnIndex * _components;
                int rawIndex = PdfImageRgbaUpsampler.ReadRawSample(sourceRow, sampleIndex, _bitsPerComponent);
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
        private unsafe void ApplyMaskToSingleRgbaRow(byte* rowPtr)
        {
            if (_maskArray == null)
            {
                return;
            }

            // For each pixel determine if ALL components fall inside their respective [min,max] ranges.
            // If so, the pixel is masked (alpha = 0). Otherwise alpha remains opaque.
            // RGB images: we explicitly restore alpha to 255 for unmasked pixels.
            // CMYK images (_components == 4): we NEVER overwrite the 4th byte to 255 here, only set to 0 when masked.
            for (int pixelIndex = 0; pixelIndex < _width; pixelIndex++)
            {
                int baseIndex = pixelIndex * 4;
                bool allInRange = true;
                for (int componentIndex = 0; componentIndex < _components; componentIndex++)
                {
                    int maskBase = componentIndex * 2;
                    int minRaw = _maskArray[maskBase];
                    int maxRaw = _maskArray[maskBase + 1];
                    int value = rowPtr[baseIndex + componentIndex];
                    if (value < minRaw || value > maxRaw)
                    {
                        allInRange = false;
                        break;
                    }
                }

                if (allInRange)
                {
                    // Masked pixel -> alpha zero.
                    rowPtr[baseIndex + 3] = 0;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ApplyDecodeMappingRgbaRow(byte* rowPtr)
        {
            if (_decodeArray == null)
            {
                return;
            }
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                int baseIndex = columnIndex * 4;
                for (int componentIndex = 0; componentIndex < _components; componentIndex++)
                {
                    byte value = rowPtr[baseIndex + componentIndex];
                    byte mapped = MapDecodedByte(value, componentIndex);
                    rowPtr[baseIndex + componentIndex] = mapped;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void ApplyDecodeMappingSingleComponentRow(byte* rowPtr)
        {
            if (_decodeArray == null)
            {
                return;
            }
            for (int columnIndex = 0; columnIndex < _width; columnIndex++)
            {
                byte value = rowPtr[columnIndex];
                rowPtr[columnIndex] = MapDecodedByte(value, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte MapDecodedByte(int expanded, int componentIndex)
        {
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
