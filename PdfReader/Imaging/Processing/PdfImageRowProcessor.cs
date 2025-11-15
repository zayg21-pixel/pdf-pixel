using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using PdfReader.Rendering.Color;
using System.Runtime.CompilerServices;
using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Sampling;

namespace PdfReader.Imaging.Processing;

/// <summary>
/// Row-oriented image post processor that converts already decoded sample rows into final output buffers.
/// </summary>
internal sealed class PdfImageRowProcessor : IDisposable
{
    private enum OutputMode
    {
        Gray,
        Rgba,
        RgbaColorApplied
    }

    private readonly PdfImage _image;
    private readonly PdfColorSpaceConverter _converter;
    private readonly ILogger _logger;

    private readonly int _width;
    private readonly int _height;
    private readonly int _bitsPerComponent;
    private readonly int _components;

    private readonly OutputMode _outputMode;
    private readonly IRowUpsampler _rowUpsampler;

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


        if (ShouldConvertColor(_image))
        {
            _outputMode = OutputMode.RgbaColorApplied;
        }
        else
        {
            _outputMode = _components == 1 || _bitsPerComponent == 16 ? OutputMode.Gray : OutputMode.Rgba;

            switch (_outputMode)
            {
                case OutputMode.Gray:
                {
                    bool upscale = _converter is not IndexedConverter;
                    _rowUpsampler = GrayRowUpsamplerFactory.Create(_width, _bitsPerComponent, upscale);
                    break;
                }
                case OutputMode.Rgba:
                {
                    _rowUpsampler = RgbaRowUpsamplerFactory.Create(_width, _components, _bitsPerComponent);
                    break;
                }
            }
        }
    }

    public static bool ShouldConvertColor(PdfImage image)
    {
        var converter = image.ColorSpaceConverter;

        if (converter == null)
        {
            return false;
        }

        // non-standard number of components, always convert color
        if (converter.Components != 1 && converter.Components != 3 && converter.Components != 4)
        {
            return true;
        }

        // can't apply mask as color filter if more than 3 components, no space for alpha
        if (converter.Components > 3 && image.MaskArray?.Length > 0)
        {
            return true;
        }

        // Always convert DeviceN and Separation to apply color correctly
        if (converter is DeviceNColorSpaceConverter || converter is SeparationColorSpaceConverter)
        {
            return true;
        }

        return false;
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

        switch (_outputMode)
        {
            case OutputMode.Gray:
                // Always output Gray8, even for 16-bit input (downsampled)
                _rowStride = _width; // 1 byte per pixel
                break;
            case OutputMode.RgbaColorApplied:
                // Full decode to RGBA 8-bit: 4 bytes per pixel
                _rowStride = _width * 4;
                break;
            default:
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
                break;
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

        if (_outputMode == OutputMode.RgbaColorApplied)
        {
            WriteWithFullColor(rowIndex, decodedRow);
            return;
        }

        var nativeRef = new NativeRef<byte>(_buffer);
        ref byte destRow = ref Unsafe.Add(ref nativeRef.Value, rowIndex * _rowStride);

        _rowUpsampler.Upsample(ref decodedRow[0], ref destRow);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColor(int rowIndex, Span<byte> decodedRow)
    {
        var nativeRef = new NativeRef<byte>(_buffer);
        ref byte destRowByte = ref Unsafe.Add(ref nativeRef.Value, rowIndex * _rowStride);
        ref Rgba destRowColor = ref Unsafe.As<byte, Rgba>(ref destRowByte);

        int width = _width;
        int componentCount = _components;
        int bitsPerComponent = _bitsPerComponent;
        PdfRenderingIntent intent = _image.RenderingIntent;

        float[] componentValues = new float[componentCount];

        bool applyDecode = _image.DecodeArray != null && _image.DecodeArray.Length == componentCount * 2;
        float[] decodeArray = _image.DecodeArray; // May be null when applyDecode is false.

        bool applyMask = _image.MaskArray != null && _image.MaskArray.Length == componentCount * 2;
        int[] maskArray = _image.MaskArray; // Raw sample code ranges (min,max) per component.

        int byteCursor = 0;
        int bitOffset = 0; // Bit cursor for packed path (1,2,4).

        int maxCode = (1 << bitsPerComponent) - 1;
        float scale = 1f / maxCode;

        for (int x = 0; x < width; x++)
        {
            bool maskMatch = applyMask;

            for (int c = 0; c < componentCount; c++)
            {
                int sample;
                if (bitsPerComponent == 16)
                {
                    int hi = decodedRow[byteCursor];
                    int lo = decodedRow[byteCursor + 1];
                    sample = hi << 8 | lo; // Big-endian.
                    byteCursor += 2;
                }
                else if (bitsPerComponent == 8)
                {
                    sample = decodedRow[byteCursor];
                    byteCursor += 1;
                }
                else
                {
                    int byteIndex = bitOffset >> 3;
                    int bitInByte = bitOffset & 7;
                    int shift = 8 - bitInByte - bitsPerComponent;
                    sample = decodedRow[byteIndex] >> shift & maxCode;
                    bitOffset += bitsPerComponent;
                }

                if (applyMask && maskMatch)
                {
                    int minCode = maskArray[c * 2];
                    int maxCodeRange = maskArray[c * 2 + 1];

                    if (sample < minCode || sample > maxCodeRange)
                    {
                        maskMatch = false; // Early reject for this pixel.
                    }
                }

                float value01 = sample * scale; // Normalize to 0..1.

                if (applyDecode)
                {
                    int di = c * 2;
                    float dMin = decodeArray[di];
                    float dMax = decodeArray[di + 1];
                    value01 = dMin + value01 * (dMax - dMin);
                }

                componentValues[c] = value01;
            }

            SKColor pixel = _converter.ToSrgb(componentValues, intent);
            if (applyMask && maskMatch)
            {
                pixel = new SKColor(pixel.Red, pixel.Green, pixel.Blue, 0);
            }

            Unsafe.Add(ref destRowColor, x) = new Rgba(pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha);
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
            case OutputMode.RgbaColorApplied:
            {
                colorType = SKColorType.Rgba8888;
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
