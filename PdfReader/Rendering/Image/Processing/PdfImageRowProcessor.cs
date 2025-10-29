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
    /// </summary>
    internal sealed class PdfImageRowProcessor : IDisposable
    {
        private enum OutputMode
        {
            Gray,
            Rgba
        }

        private readonly PdfImage _image;
        private readonly PdfColorSpaceConverter _converter;
        private readonly ILogger _logger;

        private readonly int _width;
        private readonly int _height;
        private readonly int _bitsPerComponent;
        private readonly int _components;

        private readonly OutputMode _outputMode;
        private readonly IRgbaRowDecoder _rgbaRowDecoder;
        private readonly IGrayRowDecoder _grayRowDecoder;

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

            _outputMode = (_components == 1 || _bitsPerComponent == 16) ? OutputMode.Gray : OutputMode.Rgba;

            switch (_outputMode)
            {
                case OutputMode.Gray:
                {
                    bool upscale = _converter is not IndexedConverter;
                    _grayRowDecoder = GrayRowDecoderFactory.Create(_width, _bitsPerComponent, upscale);
                    break;
                }
                case OutputMode.Rgba:
                {
                    _rgbaRowDecoder = RgbaRowDecoderFactory.Create(_width, _components, _bitsPerComponent);
                    break;
                }
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

            if (_outputMode == OutputMode.Gray)
            {
                // Always output Gray8, even for 16-bit input (downsampled)
                _rowStride = _width; // 1 byte per pixel
            }
            else
            {
                if (_bitsPerComponent == 16)
                {
                    // RGBA 16-bit: 8 bytes per pixel
                    _rowStride = _width * 8;
                }
                else
                {
                    // RGBA 8-bit: 4 bytes per pixel
                    _rowStride = _width * 4;
                }
            }

            long totalBytes = (long)_rowStride * _height;
            _buffer = Marshal.AllocHGlobal(new IntPtr(totalBytes));
            _initialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRow(int rowIndex, Span<byte> decodedRow)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("InitializeBuffer must be called before WriteRow.");
            }

            if (rowIndex < 0 || rowIndex >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
            if (_completed)
            {
                throw new InvalidOperationException("Image already completed.");
            }

            var nativeRef = new NativeRef<byte>(_buffer);
            ref byte destRow = ref Unsafe.Add(ref nativeRef.Value, rowIndex * _rowStride);

            switch (_outputMode)
            {
                case OutputMode.Gray:
                    {
                        // Always decode to 8-bit gray, even for 16-bit input
                        _grayRowDecoder.Decode(ref decodedRow[0], ref destRow);
                        break;
                    }
                case OutputMode.Rgba:
                    {
                        _rgbaRowDecoder.Decode(ref decodedRow[0], ref destRow);
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

            SKColorType colorType;
            SKAlphaType alphaType = SKAlphaType.Premul;

            switch (_outputMode)
            {
                case OutputMode.Gray:
                {
                    colorType = SKColorType.Gray8;
                    break;
                }
                default:
                {
                    if (_bitsPerComponent == 16)
                    {
                        colorType = SKColorType.Rgba16161616;
                    }
                    else
                    {
                        colorType = SKColorType.Rgba8888;
                    }
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
