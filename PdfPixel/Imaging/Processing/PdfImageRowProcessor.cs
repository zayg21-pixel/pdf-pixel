using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Png;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

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
    private readonly PdfGraphicsState _state;
    private readonly SKCanvas _canvas;

    private readonly int _bitsPerComponent;
    private readonly int _components;

    private readonly OutputMode _outputMode;
    private readonly PngImageBuilder _pngBuilder;

    private readonly IRgbaSampler _sampler;
    private byte[] _rgbaBuffer;
    private bool _initialized;
    private bool _completed;

    private readonly int _width;
    private readonly int _height;

    private readonly IRowConverter _rowConverter;
    private byte[] _convertedRowBuffer;

    public PdfImageRowProcessor(PdfImage image, ILogger logger, PdfGraphicsState state, SKCanvas canvas)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _canvas = canvas;

        var sourceWidth = image.Width;
        var sourceHeight = image.Height;
        _bitsPerComponent = image.BitsPerComponent;
        _converter = image.ColorSpaceConverter ?? throw new InvalidOperationException("Color space converter must not be null for row processing.");

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentException("Image dimensions must be positive.");
        }
        if (_bitsPerComponent != 1 && _bitsPerComponent != 2 && _bitsPerComponent != 4 && _bitsPerComponent != 8 && _bitsPerComponent != 16)
        {
            throw new NotSupportedException("Row processor supports 1,2,4,8,16 bits per component only.");
        }

        _components = _converter.Components;

        var downscaleSize = state.RenderingParameters.GetScaledSize(new SKSizeI(sourceWidth, sourceHeight), state.CTM);
        bool isIndexed = _converter is IndexedConverter;

        if (!isIndexed && !state.RenderingParameters.IsType3Rendering && downscaleSize.HasValue)
        {
            _width = downscaleSize.Value.Width;
            _height = downscaleSize.Value.Height;

            _rowConverter = new AveragingDownsampleRowConverter(_components, _bitsPerComponent, sourceWidth, _width, sourceHeight, _height);
        }
        else
        {
            _width = sourceWidth;
            _height = sourceHeight;
        }

        if (ShouldConvertColor(_image))
        {
            _sampler = _image.ColorSpaceConverter.GetRgbaSampler(_image.RenderingIntent, _state.FullTransferFunction);
            _outputMode = OutputMode.RgbaColorApplied;
            _pngBuilder = new PngImageBuilder(4, 8, _width, _height);
            _pngBuilder.Init(null, null);
        }
        else
        {
            _outputMode = OutputMode.Default;

            int outputBitsPerComponent = _rowConverter?.BitsPerComponent ?? _bitsPerComponent;

            _pngBuilder = new PngImageBuilder(_components, outputBitsPerComponent, _width, _height);

            RgbaPacked[] palette = null;
            ReadOnlyMemory<byte> iccProfile = ReadOnlyMemory<byte>.Empty;

            if (_image.ColorSpaceConverter is IndexedConverter indexed)
            {
                palette = indexed.BuildPackedPalette(_image.RenderingIntent, state.FullTransferFunction);
            }
            else if (_components == 1 && outputBitsPerComponent <= 8)
            {
                palette = BuildSingleChannelPalette(_image, state, outputBitsPerComponent);
            }

            if (_image.ColorSpaceConverter is IccBasedConverter iccBased && iccBased.Profile?.Bytes != null)
            {
                iccProfile = iccBased.Profile.Bytes;
            }
            _pngBuilder.Init(palette, iccProfile);
        }
    }

    public static RgbaPacked[] BuildSingleChannelPalette(PdfImage image, PdfGraphicsState state, int outputBitsPerComponent)
    {
        if (image.ColorSpaceConverter is DeviceGrayConverter)
        {
            return null;
        }

        var sampler = image.ColorSpaceConverter.GetRgbaSampler(image.RenderingIntent, state.FullTransferFunction);
        int maxCode = (1 << outputBitsPerComponent) - 1;
        int paletteSize = maxCode + 1;
        var palette = new RgbaPacked[paletteSize];
        Span<float> comps = stackalloc float[1];

        for (int code = 0; code < paletteSize; code++)
        {
            float value01 = maxCode == 0 ? 0f : (float)code / maxCode;
            comps[0] = value01;
            palette[code] = ColorVectorUtilities.From01ToRgba(sampler.Sample(comps));
        }

        return palette;
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

        // Indexed can be represented directly
        if (converter is IndexedConverter)
        {
            return false;
        }

        // ICC-based can be represented directly only when profile is valid
        if (converter is IccBasedConverter)
        {
            // TODO: Skia seems to ignore ICC profiles in images, so we always convert for now.
            return true;
            //using var skiaIcc = SKColorSpace.CreateIcc(iccBased.Profile.Bytes);
            //if (skiaIcc == null)
            //{
            //    return true;
            //}

            //return false;
        }

        // Device RGB and Device Gray can be represented directly
        if (converter is DeviceRgbConverter || converter is DeviceGrayConverter)
        {
            return false;
        }

        // Special-case: single-component color spaces without decode/mask and <=8 bpc
        if (converter.Components == 1 && image.BitsPerComponent <= 8)
        {
            return false;
        }

        // All other color spaces require conversion (CMYK, Lab, DeviceN with >1 component, Separation with alternate not single channel, etc.)
        return true;
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
            case OutputMode.Default:
                break;
        }

        if (_rowConverter != null)
        {
            int outputBitsPerComponent = _rowConverter.BitsPerComponent;
            int outLen = (_width * _components * outputBitsPerComponent + 7) / 8;
            _convertedRowBuffer = new byte[outLen];
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

        if (_rowConverter != null)
        {
            if (!_rowConverter.TryConvertRow(rowIndex, decodedRow, _convertedRowBuffer))
            {
                return;
            }
            decodedRow = _convertedRowBuffer;
        }

        if (_outputMode == OutputMode.RgbaColorApplied)
        {
            WriteWithFullColor(rowIndex, decodedRow);
            return;
        }

        _pngBuilder.WritePngImageRow(decodedRow);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColor(int rowIndex, Span<byte> decodedRow)
    {
        ref byte destRowByte = ref _rgbaBuffer[0];
        ref RgbaPacked destRowColor = ref Unsafe.As<byte, RgbaPacked>(ref destRowByte);
        var bitReader = new UintBitReader(decodedRow);

        int width = _width;
        int componentCount = _components;
        int bitsPerComponent = _bitsPerComponent;

        Span<float> componentValues = stackalloc float[componentCount];


        bool applyDecode = _image.DecodeArray != null && _image.DecodeArray.Length == componentCount * 2;
        float[] decodeArray = _image.DecodeArray; // May be null when applyDecode is false.

        bool applyMask = _image.MaskArray != null && _image.MaskArray.Length == componentCount * 2;
        int[] maskArray = _image.MaskArray; // Raw sample code ranges (min,max) per component.

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
                uint sample = bitReader.ReadBits(bitsPerComponent);

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
            var colorVector = _sampler.Sample(componentValues);
            ColorVectorUtilities.Load01ToRgba(colorVector, ref destinationPixel);

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
