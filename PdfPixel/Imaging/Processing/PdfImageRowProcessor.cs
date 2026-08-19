using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
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

    /// <summary>
    /// Where a row's color comes from. Palette expands to color before the resampler runs, because
    /// palette indexes cannot be averaged; every other route resamples first, at the source bit depth.
    /// The remaining routes differ only in how their samples are laid out.
    /// </summary>
    private enum RowPipeline
    {
        Palette,
        Transformed,
        DirectGray,
        DirectRgb
    }

    /// <summary>
    /// The optional steps that run while a row is written. Every pipeline reads the same set, and a
    /// stage folded into a palette at construction is cleared here so the set stays true at row time.
    /// </summary>
    [Flags]
    private enum RowStages
    {
        None = 0,
        Decode = 1 << 0,
        ColorKeyMask = 1 << 1,
        AlphaInterleaved = 1 << 2,
        AlphaPlane = 1 << 3,
        Matte = 1 << 4
    }

    private const RowStages AlphaStages = RowStages.AlphaInterleaved | RowStages.AlphaPlane;
    private const RowStages PaletteFoldedStages = RowStages.Decode | RowStages.ColorKeyMask;

    private readonly PdfImageRowDecodingParameters _parameters;
    private readonly PdfColorSpaceConverter _converter;
    private readonly ILogger _logger;

    private readonly RowPipeline _pipeline;
    private readonly RowStages _stages;

    private readonly int _components;
    private readonly int _resampleComponents;

    private readonly PdfImageColorFormat _colorFormat;
    private readonly PdfImageAlphaType _alphaType;
    private readonly Vector4 _backdrop;
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

    // Same geometry as _rowConverter, over the single-channel alpha plane, so both flush the same rows.
    private readonly IRowConverter? _alphaRowConverter;
    private byte[]? _convertedAlphaBuffer;

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
        _decodeRanges = parameters.Decode ?? Array.Empty<PdfRange>();
        _maskArray = parameters.MaskArray ?? Array.Empty<int>();
        _stages = GetRowStages(parameters);

        // Anything that has to reach a colour space converter rules out the direct routes, which do
        // nothing but relayout their samples. Selecting the route also builds what it needs to run.
        bool requiresColorConversion = (_stages & PaletteFoldedStages) != 0 || RequiresColorTransform(parameters);

        if (parameters.HasImageMask)
        {
            _pipeline = RowPipeline.Palette;
            _indexedPalette = BuildStencilMaskPalette(parameters);
        }
        else if (requiresColorConversion)
        {
            if (parameters.ColorSpaceConverter is PdfIndexedColorSpaceConverter indexedConverter)
            {
                _pipeline = RowPipeline.Palette;
                _indexedPalette = indexedConverter.BuildPackedPalette(_parameters.RenderingIntent, _parameters.Context.TransferFunction);
            }
            else if (_components == 1 && bitsPerComponent <= 8 && (_stages & RowStages.AlphaInterleaved) == 0)
            {
                _pipeline = RowPipeline.Palette;
                _indexedPalette = BuildPackedPalette(_parameters, bitsPerComponent);

                if ((_stages & RowStages.Decode) != 0)
                {
                    int maxCode = (1 << bitsPerComponent) - 1;
                    _decodeRanges = new PdfRange[] { new(_decodeRanges[0].Min * maxCode, _decodeRanges[0].Max * maxCode) };
                }
            }
            else
            {
                // Normalize only when Decode denormalizes samples to real component values below.
                bool applyDecode = (_stages & RowStages.Decode) != 0;
                _pipeline = RowPipeline.Transformed;
                _sampler = _parameters.ColorSpaceConverter.GetRgbaSampler(_parameters.RenderingIntent, _parameters.Context.TransferFunction, normalize: applyDecode);
            }
        }
        else
        {
            _pipeline = (_components == 1) ? RowPipeline.DirectGray : RowPipeline.DirectRgb;
        }

        int resampleSourceBitsPerComponent;

        if (_pipeline == RowPipeline.Palette)
        {
            // The palette has already produced 8-bit RGBA, so the resampler reads that instead of samples.
            _indexedBitsPerComponent = bitsPerComponent;
            _resampleComponents = 4;
            resampleSourceBitsPerComponent = NormalizedBitsPerComponent;
        }
        else
        {
            _resampleComponents = _components + (((_stages & RowStages.AlphaInterleaved) != 0) ? 1 : 0);
            resampleSourceBitsPerComponent = bitsPerComponent;
        }

        // The palette route merges its alpha before resampling, so only the other routes need a
        // second converter to bring the alpha plane to the output grid in lockstep with the color.
        bool needsAlphaRowConverter = (_stages & RowStages.AlphaPlane) != 0 && _pipeline != RowPipeline.Palette;

        if (parameters.DownscaledSize.HasValue)
        {
            _width = parameters.DownscaledSize.Value.Width;
            _height = parameters.DownscaledSize.Value.Height;
            _rowConverter = new AveragingDownsampleRowConverter(_resampleComponents, resampleSourceBitsPerComponent, sourceWidth, _width, sourceHeight, _height);

            if (needsAlphaRowConverter)
            {
                _alphaRowConverter = new AveragingDownsampleRowConverter(1, NormalizedBitsPerComponent, sourceWidth, _width, sourceHeight, _height);
            }
        }
        else
        {
            _width = sourceWidth;
            _height = sourceHeight;
            _rowConverter = new SampleNormalizingRowConverter(_resampleComponents, resampleSourceBitsPerComponent, _width);

            if (needsAlphaRowConverter)
            {
                _alphaRowConverter = new SampleNormalizingRowConverter(1, NormalizedBitsPerComponent, _width);
            }
        }

        // A single gray channel with no alpha stage to run is the only case that stays out of RGBA.
        if (_pipeline == RowPipeline.DirectGray && (_stages & AlphaStages) == 0)
        {
            _colorFormat = PdfImageColorFormat.Gray;
            _alphaType = PdfImageAlphaType.Opaque;
            _rowBytes = _width;
        }
        else
        {
            // Colour key masking and the folded palette write zero alpha into buffers with no alpha source.
            _colorFormat = PdfImageColorFormat.Rgba;
            _alphaType = PdfImageAlphaType.Unpremultiplied;
            _rowBytes = _width * 4;

            float[]? matte = parameters.Matte;

            if (matte != null)
            {
                PdfColor backdropColor = _converter.ToSrgb(matte, parameters.RenderingIntent, parameters.Context.TransferFunction);
                _backdrop = new Vector4(backdropColor.Red, backdropColor.Green, backdropColor.Blue, 1f);
                _stages |= RowStages.Matte;
            }
        }

        if (_pipeline == RowPipeline.Palette)
        {
            _scale = 1f / ((1 << _indexedBitsPerComponent) - 1);
        }
        else
        {
            _scale = 1f / ((1 << NormalizedBitsPerComponent) - 1);
        }

        if (_indexedPalette != null)
        {
            // Decode and the colour key mask move into the palette entries, so they no longer run per row.
            _indexedPalette = FoldDecodeAndMaskIntoPalette(_indexedPalette);
            _stages &= ~PaletteFoldedStages;
        }
    }

    /// <summary>
    /// Allocates the destination buffer based on the selected pipeline.
    /// Must be called before any <see cref="WriteRow"/> invocation.
    /// </summary>
    public void InitializeBuffer()
    {
        if (_initialized)
        {
            return;
        }

        _decodedImage = new PdfDecodedImage(_width, _height, _colorFormat, _alphaType);

        if (_alphaRowConverter != null)
        {
            _convertedAlphaBuffer = new byte[_width];
        }

        switch (_pipeline)
        {
            case RowPipeline.Palette:
            {
                // Filled before the resampler runs, so it spans the source grid.
                _rgbaBuffer = new byte[_parameters.Width * 4];
                break;
            }
            case RowPipeline.Transformed:
            {
                _rgbaBuffer = new byte[_width * 4];
                break;
            }
            case RowPipeline.DirectGray:
            case RowPipeline.DirectRgb:
                break;
        }

        _convertedRowBuffer = new byte[(_width * _resampleComponents) + 1];

        _initialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRow(int rowIndex, in Span<byte> decodedRow, in ReadOnlySpan<byte> alphaRow)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("InitializeBuffer must be called before WriteRow.");
        }

        switch (_pipeline)
        {
            case RowPipeline.Palette:
            {
                WritePaletteRow(rowIndex, decodedRow, alphaRow);
                break;
            }
            case RowPipeline.Transformed:
            {
                WriteTransformedRow(rowIndex, decodedRow, alphaRow);
                break;
            }
            case RowPipeline.DirectGray:
            {
                WriteDirectGrayRow(rowIndex, decodedRow, alphaRow);
                break;
            }
            case RowPipeline.DirectRgb:
            {
                WriteDirectRgbRow(rowIndex, decodedRow, alphaRow);
                break;
            }
        }
    }

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

    private PdfDecodedImage GetDecodedImage() => _decodedImage ?? throw new InvalidOperationException("Not initialized.");
}
