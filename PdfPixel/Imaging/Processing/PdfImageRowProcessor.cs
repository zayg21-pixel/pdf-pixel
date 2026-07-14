using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Models;
using PdfPixel.Parsing;
using SkiaSharp;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Row-oriented image post processor that converts already decoded sample rows into final output buffers.
/// </summary>
internal sealed partial class PdfImageRowProcessor : IDisposable
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
    private readonly SKImageInfo _imageInfo;

    private readonly ColorTransformSampler? _sampler;
    private readonly RgbaPacked[]? _indexedPalette;
    private byte[]? _rgbaBuffer;
    private byte[]? _pixelBuffer;
    private int _pixelBufferOffset;
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
                _indexedPalette = indexedConverter.BuildPackedPalette(_parameters.RenderingIntent, _parameters.Context.FullTransferFunction);
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
                _sampler = _parameters.ColorSpaceConverter.GetRgbaSampler(_parameters.RenderingIntent, _parameters.Context.FullTransferFunction);
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
            _imageInfo = new SKImageInfo(_width, _height, SKColorType.Gray8, SKAlphaType.Opaque);
        }
        else
        {
            _imageInfo = new SKImageInfo(_width, _height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        }

        if (_outputMode == OutputMode.IndexedRgbaColorConverted)
        {
            _scale = 1f / ((1 << _indexedBitsPerComponent) - 1);
        }
        else
        {
            _scale = 1f / ((1 << NormalizedBitsPerComponent) - 1);
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

        _pixelBuffer = ArrayPool<byte>.Shared.Rent(_imageInfo.RowBytes * _height);

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
                if (_stages == ProcessingStages.SampleColor)
                {
                    WriteIndexedRowSampleOnly(decodedRow);
                }
                else
                {
                    WriteIndexedRow(decodedRow);
                }

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
    private void WriteIndexedRowSampleOnly(in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _indexedPalette == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        RgbaPacked[] palette = _indexedPalette;
        ref RgbaPacked paletteRef = ref palette[0];
        var paletteSize = (uint)palette.Length;
        uint paletteMin = paletteSize - 1;
        int pixelCount = _parameters.Width;
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref _rgbaBuffer[0]);
        UintBitReaderFixedLength bitReader = new(decodedRow, _indexedBitsPerComponent);

        for (int x = 0; x < pixelCount; x++)
        {
            uint sample = Math.Min(bitReader.Read(), paletteMin);
            destPixel = Unsafe.Add(ref paletteRef, sample);
            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteIndexedRow(in Span<byte> decodedRow)
    {
        if (_rgbaBuffer == null || _indexedPalette == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        // TODO: [MEDIUM] optimize further, see ColorMinAndScale
        RgbaPacked[] palette = _indexedPalette;
        ref RgbaPacked paletteRef = ref palette[0];
        var paletteSize = (uint)palette.Length;
        uint paletteMin = paletteSize - 1;
        int pixelCount = _parameters.Width;
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref _rgbaBuffer[0]);
        UintBitReaderFixedLength bitReader = new(decodedRow, _indexedBitsPerComponent);

        bool applyDecode = (_stages & ProcessingStages.Decode) != 0;
        bool applyMask = (_stages & ProcessingStages.Mask) != 0;

        for (int x = 0; x < pixelCount; x++)
        {
            uint sample = bitReader.Read();

            if (applyDecode)
            {
                PdfRange decodeRange = _decodeRanges[0];
                sample = (uint)Math.Max(0, decodeRange.Min + (sample * decodeRange.Range * _scale));
            }

            sample = Math.Min(sample, paletteMin);

            destPixel = palette[sample];

            if (applyMask)
            {
                int minCode = _maskArray[0];
                int maxCodeRange = _maskArray[1];

                if (sample >= (uint)minCode && sample <= (uint)maxCodeRange)
                {
                    destPixel.A = 0;
                }
            }

            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
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
        int rowBytes = _imageInfo.RowBytes;
        source.Slice(0, rowBytes).CopyTo(_pixelBuffer.AsSpan(_pixelBufferOffset, rowBytes));
        _pixelBufferOffset += rowBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteGrayAlphaRow(in ReadOnlySpan<byte> normalizedRow)
    {
        if (_pixelBuffer == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref _pixelBuffer[_pixelBufferOffset]);

        for (int x = 0; x < _width; x++)
        {
            int offset = x * 2;
            byte gray = Unsafe.Add(ref source, offset);
            byte alpha = Unsafe.Add(ref source, offset + 1);
            Unsafe.Add(ref destPixel, x) = (uint)(gray | (gray << 8) | (gray << 16) | (alpha << 24));
        }

        _pixelBufferOffset += _imageInfo.RowBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteRgba8Row(in ReadOnlySpan<byte> normalizedRow)
    {
        if (_pixelBuffer == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref _pixelBuffer[_pixelBufferOffset]);

        for (int x = 0; x < _width; x++)
        {
            uint rgb = Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, x * 3));
            Unsafe.Add(ref destPixel, x) = rgb | 0xFF000000;
        }

        _pixelBufferOffset += _imageInfo.RowBytes;
    }

    /// <summary>
    /// Returns an SKImage built from the pixel buffer.
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

        int totalBytes = _imageInfo.RowBytes * _height;
        return SKImage.FromPixelCopy(_imageInfo, _pixelBuffer.AsSpan(0, totalBytes));
    }

    public void Dispose()
    {
        if (_pixelBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_pixelBuffer);
            _pixelBuffer = null;
        }
    }
}
