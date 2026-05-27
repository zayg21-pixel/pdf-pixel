using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Imaging.Png;
using PdfPixel.Parsing;
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

    private readonly PdfImageRowDecodingParameters _parameters;
    private readonly PdfColorSpaceConverter _converter;
    private readonly ILogger _logger;

    private readonly int _bitsPerComponent;
    private readonly int _components;

    private readonly OutputMode _outputMode;
    private readonly PngImageBuilder _pngBuilder;

    private readonly ColorTransformSampler? _sampler;
    private byte[]? _rgbaBuffer;
    private bool _initialized;
    private bool _completed;

    private readonly int _width;
    private readonly int _height;

    private readonly AveragingDownsampleRowConverter? _rowConverter;
    private byte[]? _convertedRowBuffer;

    private readonly bool _applyDecode;
    private readonly float[]? _decodeArray;
    private readonly bool _applyMask;
    private readonly int[]? _maskArray;
    private readonly int _maxCode;
    private readonly float _scale;

    public PdfImageRowProcessor(PdfImageRowDecodingParameters parameters, ILogger logger)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        int sourceWidth = parameters.Width;
        int sourceHeight = parameters.Height;
        _bitsPerComponent = parameters.BitsPerComponent;
        _converter = parameters.ColorSpaceConverter ?? throw new InvalidOperationException("Color space converter must not be null for row processing.");

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentException("Image dimensions must be positive.");
        }

        if (_bitsPerComponent > 16)
        {
            throw new NotSupportedException($"Row processor supports up to 16 bits per component (got {_bitsPerComponent}).");
        }

        _components = _converter.Components;

        if (parameters.DownscaledSize.HasValue)
        {
            _width = parameters.DownscaledSize.Value.Width;
            _height = parameters.DownscaledSize.Value.Height;

            _rowConverter = new AveragingDownsampleRowConverter(_components, _bitsPerComponent, sourceWidth, _width, sourceHeight, _height);
            _bitsPerComponent = _rowConverter.BitsPerComponent;
        }
        else
        {
            _width = sourceWidth;
            _height = sourceHeight;
        }

        if (ShouldConvertColor(_parameters))
        {
            _sampler = _parameters.ColorSpaceConverter.GetRgbaSampler(_parameters.RenderingIntent, _parameters.Context.FullTransferFunction);
            _outputMode = OutputMode.RgbaColorApplied;
            _pngBuilder = new PngImageBuilder(4, 8, _width, _height);
            _pngBuilder.Init(null, null);
        }
        else
        {
            _outputMode = OutputMode.Default;

            _pngBuilder = new PngImageBuilder(_components, _bitsPerComponent, _width, _height);

            RgbaPacked[]? palette = null;

            if (_parameters.ColorSpaceConverter is IndexedConverter indexed)
            {
                palette = indexed.BuildPackedPalette(_parameters.RenderingIntent, _parameters.Context.FullTransferFunction);
            }
            else if (_components == 1 && _bitsPerComponent <= 8)
            {
                palette = BuildSingleChannelPalette(_parameters, _bitsPerComponent);
            }

            _pngBuilder.Init(palette, default);
        }

        _maxCode = (1 << _bitsPerComponent) - 1;
        _scale = (_converter is IndexedConverter) ? 1f : 1f / _maxCode;
        _applyDecode = _parameters.DecodeArray != null && _parameters.DecodeArray.Length == _components * 2;
        _decodeArray = _parameters.DecodeArray;
        _applyMask = _parameters.MaskArray != null && _parameters.MaskArray.Length == _components * 2;
        _maskArray = _parameters.MaskArray;
    }

    public static RgbaPacked[]? BuildSingleChannelPalette(PdfImageRowDecodingParameters parameters, int outputBitsPerComponent)
    {
        if (parameters.ColorSpaceConverter is DeviceGrayConverter)
        {
            return null;
        }

        ColorTransformSampler sampler = parameters.ColorSpaceConverter.GetRgbaSampler(parameters.RenderingIntent, parameters.Context.FullTransferFunction);
        int maxCode = (1 << outputBitsPerComponent) - 1;
        int paletteSize = maxCode + 1;
        var palette = new RgbaPacked[paletteSize];
        Span<float> comps = stackalloc float[1];

        for (int code = 0; code < paletteSize; code++)
        {
            float value01 = (maxCode == 0) ? 0f : (float)code / maxCode;
            comps[0] = value01;
            palette[code] = sampler.Sample(comps).From01ToRgba();
        }

        return palette;
    }

    public static bool ShouldConvertColor(PdfImageRowDecodingParameters parameters)
    {
        PdfColorSpaceConverter converter = parameters.ColorSpaceConverter;

        if (converter == null)
        {
            return false;
        }

        // stencil mask, never convert color, they should have no color info
        if (parameters.HasImageMask)
        {
            return false;
        }

        // If decode is present, we should convert color after decode, so we need full color conversion
        if (parameters.DecodeArray != null)
        {
            return true;
        }

        // If mask is present, we should convert color after mask application, so we need full color conversion
        if (parameters.MaskArray != null)
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
            return true;
        }

        // Device RGB and Device Gray can be represented directly
        if (converter is DeviceRgbConverter || converter is DeviceGrayConverter)
        {
            return false;
        }

        // Special-case: single-component color spaces without decode/mask and <=8 bpc
        if (converter.Components == 1 && parameters.BitsPerComponent <= 8)
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
                {
                    _rgbaBuffer = new byte[_width * 4];
                    break;
                }
            case OutputMode.Default:
                break;
        }

        if (_rowConverter != null)
        {
            int outputBitsPerComponent = _rowConverter.BitsPerComponent;
            int outLen = ((_width * _components * outputBitsPerComponent) + 7) / 8;
            _convertedRowBuffer = new byte[outLen];
        }

        _initialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRow(int rowIndex, in Span<byte> decodedRow)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InitializeBuffer must be called before WriteRow.");
        }

        Span<byte> decodedRowLocal = decodedRow;

        if (_rowConverter != null)
        {
            if (!_rowConverter.TryConvertRow(rowIndex, decodedRowLocal, _convertedRowBuffer))
            {
                return;
            }

            decodedRowLocal = _convertedRowBuffer;
        }

        if (_outputMode == OutputMode.RgbaColorApplied)
        {
            WriteWithFullColor(rowIndex, decodedRowLocal);
            return;
        }

        _pngBuilder.WritePngImageRow(decodedRowLocal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColor(int rowIndex, in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _sampler == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        ref byte destRowByte = ref _rgbaBuffer[0];
        ref RgbaPacked destRowColor = ref Unsafe.As<byte, RgbaPacked>(ref destRowByte);
        UintBitReaderFixedLength bitReader = new(decodedRow, _bitsPerComponent);

        Span<float> componentValues = stackalloc float[_components];

        for (int x = 0; x < _width; x++)
        {
            bool maskMatch = _applyMask;

            for (int c = 0; c < _components; c++)
            {
                uint sample = bitReader.Read();

                if (_applyMask && maskMatch && _maskArray != null)
                {
                    int minCode = _maskArray[c * 2];
                    int maxCodeRange = _maskArray[(c * 2) + 1];

                    if (sample < minCode || sample > maxCodeRange)
                    {
                        maskMatch = false;
                    }
                }

                float value01 = sample * _scale;

                if (_applyDecode && _decodeArray != null)
                {
                    int di = c * 2;
                    float dMin = _decodeArray[di];
                    float dMax = _decodeArray[di + 1];
                    value01 = dMin + (value01 * (dMax - dMin));
                }

                componentValues[c] = value01;
            }

            ref RgbaPacked destinationPixel = ref Unsafe.Add(ref destRowColor, x);
            System.Numerics.Vector4 colorVector = _sampler.Sample(componentValues);
            ColorVectorUtilities.Load01ToRgba(colorVector, ref destinationPixel);

            if (_applyMask && maskMatch)
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

    public void Dispose() => _pngBuilder.Dispose();
}
