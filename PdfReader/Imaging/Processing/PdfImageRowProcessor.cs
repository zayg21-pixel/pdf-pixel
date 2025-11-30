using Microsoft.Extensions.Logging;
using PdfReader.Color.ColorSpace;
using PdfReader.Color.Lut;
using PdfReader.Color.Structures;
using PdfReader.Imaging.Decoding;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Png;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Imaging.Processing;

/// <summary>
/// Row-oriented image post processor that converts already decoded sample rows into final output buffers.
/// </summary>
internal sealed class PdfImageRowProcessor : IDisposable
{
    private enum OutputMode
    {
        Default,
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
    private readonly PngImageBuilder _pngBuilder;

    private readonly IRgbaSampler _sampler;
    private byte[] _rgbaBuffer;
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
            _sampler = _image.ColorSpaceConverter.GetRgbaSampler(_image.RenderingIntent);
            _outputMode = OutputMode.RgbaColorApplied;
            _pngBuilder = new PngImageBuilder(4, 8, _width, _height);
            _pngBuilder.Init(null, null);
        }
        else
        {
            _pngBuilder = new PngImageBuilder(_components, _bitsPerComponent, _width, _height);

            SKColor[] palette = null;
            ReadOnlyMemory<byte> iccProfile = ReadOnlyMemory<byte>.Empty;
            bool canApplyColorSpace = _image.DecodeArray == null && _image.MaskArray == null;

            if (_image.ColorSpaceConverter is IndexedConverter indexed)
            {
                palette = indexed.BuildPalette(_image.RenderingIntent);
            }
            if (canApplyColorSpace && _image.ColorSpaceConverter is IccBasedConverter iccBased && iccBased.Profile?.Bytes != null)
            {
                iccProfile = iccBased.Profile.Bytes;
            }
            _pngBuilder.Init(palette, iccProfile);
        }
    }

    public static bool ShouldConvertColor(PdfImage image)
    {
        var converter = image.ColorSpaceConverter;

        if (converter == null)
        {
            return false;
        }

        // stencil mask, never convert color, they should have no color info
        if (image.HasImageMask)
        {
            return false;
        }

        // non-standard number of components, always convert color, including CMYK
        if (converter.Components != 1 && converter.Components != 3)
        {
            return true;
        }

        // If decode is present, we should convert color after decode, so we need full color conversion
        if (image.DecodeArray != null)
        {
            return true;
        }

        // If mask is present, we should convert color after mask application, so we need full color conversion
        if (image.MaskArray != null)
        {
            return true;
        }

        // Standard color spaces that can be directly represented in PNG without conversion,
        // all other color spaces require conversion, e.g., DeviceCMYK, Separation, etc.
        if (!(converter is DeviceRgbConverter || converter is DeviceGrayConverter || converter is IndexedConverter || converter is IccBasedConverter))
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
            case OutputMode.RgbaColorApplied:
                _rgbaBuffer = new byte[_width * 4];
                break;
        }

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

        // Copy decoded row after filter byte
        _pngBuilder.WritePngImageRow(decodedRow);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColor(int rowIndex, Span<byte> decodedRow)
    {
        ref byte destRowByte = ref _rgbaBuffer[0];
        ref RgbaPacked destRowColor = ref Unsafe.As<byte, RgbaPacked>(ref destRowByte);

        int width = _width;
        int componentCount = _components;
        int bitsPerComponent = _bitsPerComponent;

        float[] componentValues = new float[componentCount];

        bool applyDecode = _image.DecodeArray != null && _image.DecodeArray.Length == componentCount * 2;
        float[] decodeArray = _image.DecodeArray; // May be null when applyDecode is false.

        bool applyMask = _image.MaskArray != null && _image.MaskArray.Length == componentCount * 2;
        int[] maskArray = _image.MaskArray; // Raw sample code ranges (min,max) per component.

        int byteCursor = 0;
        int bitOffset = 0; // Bit cursor for packed path (1,2,4).

        int maxCode = (1 << bitsPerComponent) - 1;
        float scale;

        if (_converter is IndexedConverter)
        {
            scale = 1f; // Indexed color spaces use direct sample codes.
        }
        else
        {
            scale = 1f / maxCode;
        }

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
            ref RgbaPacked destinationPixel = ref Unsafe.Add(ref destRowColor, x);
            _sampler.Sample(componentValues, ref destinationPixel);

            if (applyMask && maskMatch)
            {
                destinationPixel.A = 0;
            }
        }

        _pngBuilder.WritePngImageRow(_rgbaBuffer);
    }

    /// <summary>
    /// Returns an SKImage wrapping the unmanaged buffer. Ownership of buffer transfers to Skia.
    /// </summary>
    public SKImage GetDecoded()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InitializeBuffer must be called before GetSkImage.");
        }
        if (_completed)
        {
            throw new InvalidOperationException("GetSkImage already called.");
        }

        _completed = true;

        return _pngBuilder.Build();

    }

    public void Dispose()
    {
        _pngBuilder.Dispose();
    }
}
