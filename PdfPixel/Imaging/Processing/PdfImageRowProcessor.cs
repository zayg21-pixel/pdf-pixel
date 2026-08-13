using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Parsing;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Row-oriented image post processor that converts already decoded sample rows into final output buffers.
/// </summary>
internal sealed partial class PdfImageRowProcessor
{
    private const int NormalizedBitsPerComponent = 8;

    [Flags]
    private enum ProcessingStages
    {
        None = 0,
        Decode = 1 << 0,
        Mask = 1 << 1,
        SampleColor = 1 << 2
    }

    private enum OutputMode
    {
        Gray,
        Rgba,
        RgbaColorConverted,
        IndexedRgbaColorConverted
    }

    private readonly PdfImageRowDecodingParameters _parameters;
    private readonly PdfColorSpaceConverter _converter;
    private readonly ILogger _logger;

    private readonly int _components;
    private readonly int _totalComponents;
    private readonly bool _hasAlpha;

    private readonly OutputMode _outputMode;
    private readonly PdfImageColorFormat _colorFormat;
    private readonly int _rowBytes;

    private readonly ColorTransformSampler? _sampler;
    private readonly RgbaPacked[]? _indexedPalette;
    private byte[]? _rgbaBuffer;
    private PdfDecodedImage? _decodedImage;
    private int _outputRowIndex;
    private bool _initialized;
    private bool _completed;

    private readonly int _width;
    private readonly int _height;

    private readonly IRowConverter _rowConverter;
    private byte[]? _convertedRowBuffer;

    private readonly ProcessingStages _stages;
    private readonly PdfRange[] _decodeRanges;
    private readonly int[] _maskArray;
    private readonly int _indexedBitsPerComponent;
    private readonly float _scale;

    public PdfImageRowProcessor(PdfImageRowDecodingParameters parameters, ILogger logger)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        int sourceWidth = parameters.Width;
        int sourceHeight = parameters.Height;
        int bitsPerComponent = parameters.BitsPerComponent;
        _converter = parameters.ColorSpaceConverter ?? throw new InvalidOperationException("Color space converter must not be null for row processing.");

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentException("Image dimensions must be positive.");
        }

        if (bitsPerComponent > 16)
        {
            throw new NotSupportedException($"Row processor supports up to 16 bits per component (got {bitsPerComponent}).");
        }

        _components = _converter.Components;
        _hasAlpha = parameters.HasAlphaChannel;
        _totalComponents = _components + (_hasAlpha ? 1 : 0);
        _decodeRanges = parameters.Decode ?? Array.Empty<PdfRange>();
        _maskArray = parameters.MaskArray ?? Array.Empty<int>();
        _stages = GetProcessingStages(parameters);

        if (_stages != ProcessingStages.None)
        {
            if (parameters.ColorSpaceConverter is IndexedConverter indexedConverter)
            {
                _outputMode = OutputMode.IndexedRgbaColorConverted;
                _indexedPalette = indexedConverter.BuildPackedPalette(_parameters.RenderingIntent, _parameters.Context.TransferFunction);
            }
            else if (_components == 1 && bitsPerComponent <= 8 && !_hasAlpha)
            {
                _outputMode = OutputMode.IndexedRgbaColorConverted;
                _indexedPalette = BuildPackedPalette(_parameters, bitsPerComponent);

                if ((_stages & ProcessingStages.Decode) != 0)
                {
                    int maxCode = (1 << bitsPerComponent) - 1;
                    _decodeRanges = new PdfRange[] { new(_decodeRanges[0].Min * maxCode, _decodeRanges[0].Max * maxCode) };
                }
            }
            else
            {
                _outputMode = OutputMode.RgbaColorConverted;

                // Normalize only when Decode denormalizes samples to real component values below.
                bool applyDecode = (_stages & ProcessingStages.Decode) != 0;
                _sampler = _parameters.ColorSpaceConverter.GetRgbaSampler(_parameters.RenderingIntent, _parameters.Context.TransferFunction, normalize: applyDecode);
            }
        }
        else
        {
            _outputMode = (_components == 1 && !_hasAlpha) ? OutputMode.Gray : OutputMode.Rgba;
        }

        int rowConverterSourceBitsPerComponent = bitsPerComponent;

        if (_outputMode == OutputMode.IndexedRgbaColorConverted)
        {
            _indexedBitsPerComponent = bitsPerComponent;
            _totalComponents = 4;
            rowConverterSourceBitsPerComponent = NormalizedBitsPerComponent;
        }

        if (parameters.DownscaledSize.HasValue)
        {
            _width = parameters.DownscaledSize.Value.Width;
            _height = parameters.DownscaledSize.Value.Height;
            _rowConverter = new AveragingDownsampleRowConverter(_totalComponents, rowConverterSourceBitsPerComponent, sourceWidth, _width, sourceHeight, _height);
        }
        else
        {
            _width = sourceWidth;
            _height = sourceHeight;
            _rowConverter = new SampleNormalizingRowConverter(_totalComponents, rowConverterSourceBitsPerComponent, _width);
        }

        if (_outputMode == OutputMode.Gray)
        {
            _colorFormat = PdfImageColorFormat.Gray;
            _rowBytes = _width;
        }
        else
        {
            _colorFormat = PdfImageColorFormat.Rgba;
            _rowBytes = _width * 4;
        }

        if (_outputMode == OutputMode.IndexedRgbaColorConverted)
        {
            _scale = 1f / ((1 << _indexedBitsPerComponent) - 1);
        }
        else
        {
            _scale = 1f / ((1 << NormalizedBitsPerComponent) - 1);
        }

        if (_indexedPalette != null)
        {
            _indexedPalette = FoldDecodeAndMaskIntoPalette(_indexedPalette);
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

        _decodedImage = new PdfDecodedImage(_width, _height, _colorFormat);

        switch (_outputMode)
        {
            case OutputMode.IndexedRgbaColorConverted:
                {
                    int rgbaWidth = _parameters.Width;
                    _rgbaBuffer = new byte[rgbaWidth * 4];
                    break;
                }
            case OutputMode.RgbaColorConverted:
                {
                    _rgbaBuffer = new byte[_width * 4];
                    break;
                }
            case OutputMode.Gray:
            case OutputMode.Rgba:
                break;
        }

        _convertedRowBuffer = new byte[_width * _totalComponents];

        _initialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRow(int rowIndex, in Span<byte> decodedRow)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InitializeBuffer must be called before WriteRow.");
        }

        switch (_outputMode)
        {
            case OutputMode.IndexedRgbaColorConverted:
            {
                WriteIndexedRow(decodedRow);

                if (!_rowConverter.TryConvertRow(rowIndex, _rgbaBuffer, _convertedRowBuffer))
                {
                    return;
                }

                CopyRowToPixelBuffer(_convertedRowBuffer);

                break;
            }
            case OutputMode.RgbaColorConverted:
            {
                if (!_rowConverter.TryConvertRow(rowIndex, decodedRow, _convertedRowBuffer))
                {
                    return;
                }

                if (_stages == ProcessingStages.SampleColor)
                {
                    WriteWithFullColorSampleOnly(_convertedRowBuffer);
                }
                else
                {
                    WriteWithFullColor(_convertedRowBuffer);
                }

                CopyRowToPixelBuffer(_rgbaBuffer);
                break;
            }
            case OutputMode.Gray:
            {
                if (!_rowConverter.TryConvertRow(rowIndex, decodedRow, _convertedRowBuffer))
                {
                    return;
                }

                CopyRowToPixelBuffer(_convertedRowBuffer);
                break;
            }
            case OutputMode.Rgba:
            {
                if (!_rowConverter.TryConvertRow(rowIndex, decodedRow, _convertedRowBuffer))
                {
                    return;
                }

                if (_hasAlpha)
                {
                    if (_components == 1)
                    {
                        WriteGrayAlphaRow(_convertedRowBuffer);
                    }
                    else
                    {
                        CopyRowToPixelBuffer(_convertedRowBuffer);
                    }
                }
                else
                {
                    WriteRgba8Row(_convertedRowBuffer);
                }

                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIndexedRow(in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _indexedPalette == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        RgbaPacked[] palette = _indexedPalette;
        ref RgbaPacked paletteRef = ref palette[0];
        var maxPaletteIndex = (uint)(palette.Length - 1);
        int pixelCount = _parameters.Width;
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref _rgbaBuffer[0]);
        UintBitReaderFixedLength bitReader = new(decodedRow, _indexedBitsPerComponent);

        for (int x = 0; x < pixelCount; x++)
        {
            uint sample = Math.Min(bitReader.Read(), maxPaletteIndex);
            destPixel = Unsafe.Add(ref paletteRef, sample);
            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    /// <summary>
    /// Rebuilds the palette so that a raw sample read from the row selects its final pixel directly, with the
    /// /Decode mapping and the colour key mask already applied. The result is indexed by sample value and so
    /// spans the whole sample domain rather than the source palette's entry count. Returns the source palette
    /// unchanged when neither stage applies.
    /// </summary>
    /// <param name="palette">The palette built from the image's colour space.</param>
    private RgbaPacked[] FoldDecodeAndMaskIntoPalette(RgbaPacked[] palette)
    {
        bool applyDecode = (_stages & ProcessingStages.Decode) != 0;
        bool applyMask = (_stages & ProcessingStages.Mask) != 0;

        if (!applyDecode && !applyMask)
        {
            return palette;
        }

        float decodeMin = 0f;
        float decodeScale = 0f;

        if (applyDecode)
        {
            PdfRange decodeRange = _decodeRanges[0];
            decodeMin = decodeRange.Min;
            decodeScale = decodeRange.Range * _scale;
        }

        uint maskMinCode = 0;
        uint maskMaxCode = 0;

        if (applyMask)
        {
            maskMinCode = (uint)_maskArray[0];
            maskMaxCode = (uint)_maskArray[1];
        }

        var maxPaletteIndex = (uint)(palette.Length - 1);
        int sampleCount = 1 << _indexedBitsPerComponent;
        var foldedPalette = new RgbaPacked[sampleCount];

        for (int sample = 0; sample < sampleCount; sample++)
        {
            var paletteIndex = (uint)sample;

            if (applyDecode)
            {
                paletteIndex = (uint)Math.Max(0f, decodeMin + (paletteIndex * decodeScale));
            }

            paletteIndex = Math.Min(paletteIndex, maxPaletteIndex);

            RgbaPacked pixel = palette[paletteIndex];

            if (applyMask && paletteIndex >= maskMinCode && paletteIndex <= maskMaxCode)
            {
                pixel.A = 0;
            }

            foldedPalette[sample] = pixel;
        }

        return foldedPalette;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColorSampleOnly(in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _sampler == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        int pixelCount = _width;
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref _rgbaBuffer[0]);
        ref byte sourceByte = ref decodedRow[0];

        Span<float> componentValues = stackalloc float[_components];

        for (int x = 0; x < pixelCount; x++)
        {
            for (int c = 0; c < _components; c++)
            {
                componentValues[c] = sourceByte * _scale;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
            }

            ColorVectorUtilities.Load01ToRgba(_sampler.Sample(componentValues), ref destPixel);

            if (_hasAlpha)
            {
                destPixel.A = sourceByte;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
            }

            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteWithFullColor(in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _sampler == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        int pixelCount = _width;
        ref byte destRowByte = ref _rgbaBuffer[0];
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref destRowByte);
        ref byte sourceByte = ref decodedRow[0];

        Span<float> componentValues = stackalloc float[_components];

        bool applyDecode = (_stages & ProcessingStages.Decode) != 0;
        bool applyMask = (_stages & ProcessingStages.Mask) != 0;

        for (int x = 0; x < pixelCount; x++)
        {
            bool maskMatch = applyMask;

            for (int c = 0; c < _components; c++)
            {
                byte sample = sourceByte;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);

                if (applyMask && maskMatch)
                {
                    int minCode = _maskArray[c * 2];
                    int maxCodeRange = _maskArray[(c * 2) + 1];

                    if (sample < minCode || sample > maxCodeRange)
                    {
                        maskMatch = false;
                    }
                }

                float value01 = sample * _scale;

                if (applyDecode)
                {
                    value01 = _decodeRanges[c].Denormalize(value01);
                }

                componentValues[c] = value01;
            }

            Vector4 colorVector = _sampler.Sample(componentValues);
            ColorVectorUtilities.Load01ToRgba(colorVector, ref destPixel);

            if (_hasAlpha)
            {
                destPixel.A = sourceByte;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
            }

            if (applyMask && maskMatch)
            {
                destPixel.A = 0;
            }

            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyRowToPixelBuffer(in ReadOnlySpan<byte> source)
    {
        Span<byte> destRow = GetDecodedImage().GetRow(_outputRowIndex++);
        source.Slice(0, _rowBytes).CopyTo(destRow);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteGrayAlphaRow(in ReadOnlySpan<byte> normalizedRow)
    {
        Span<byte> destRow = GetDecodedImage().GetRow(_outputRowIndex++);

        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < _width; x++)
        {
            int offset = x * 2;
            byte gray = Unsafe.Add(ref source, offset);
            byte alpha = Unsafe.Add(ref source, offset + 1);
            Unsafe.Add(ref destPixel, x) = (uint)(gray | (gray << 8) | (gray << 16) | (alpha << 24));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteRgba8Row(in ReadOnlySpan<byte> normalizedRow)
    {
        Span<byte> destRow = GetDecodedImage().GetRow(_outputRowIndex++);

        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < _width; x++)
        {
            uint rgb = Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, x * 3));
            Unsafe.Add(ref destPixel, x) = rgb | 0xFF000000;
        }
    }

    private PdfDecodedImage GetDecodedImage() => _decodedImage ?? throw new InvalidOperationException("Not initialized.");

    /// <summary>
    /// Returns the decoded pixel data built from the pixel buffer. Ownership transfers to the caller,
    /// who becomes responsible for disposing it.
    /// </summary>
    public PdfDecodedImage GetDecoded()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InitializeBuffer must be called before GetDecoded.");
        }

        if (_completed)
        {
            throw new InvalidOperationException("GetDecoded already called.");
        }

        PdfDecodedImage decodedImage = GetDecodedImage();
        _completed = true;
        _decodedImage = null;

        return decodedImage;
    }
}
