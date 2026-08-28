using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Geometry;
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
        Matte = 1 << 4,
        Invert = 1 << 5
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
    private readonly int _resampleSourceBitsPerComponent;
    private readonly int _sourceBitsPerPixel;

    private readonly PdfImageColorFormat _colorFormat;
    private readonly PdfImageAlphaType _alphaType;
    private readonly Vector4 _backdrop;

    private readonly ColorTransformSampler? _sampler;
    private readonly RgbaPacked[]? _indexedPalette;

    private readonly byte[]? _paletteRgbaBuffer;
    private readonly byte[]? _convertedRowBuffer;
    private readonly byte[]? _convertedAlphaBuffer;

    private readonly bool _needsAlphaConverter;

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
        _sourceBitsPerPixel = parameters.ComponentCount * bitsPerComponent;
        _decodeRanges = parameters.Decode ?? Array.Empty<PdfRange>();
        _maskArray = parameters.MaskArray ?? Array.Empty<int>();
        _stages = GetRowStages(parameters);

        // Anything that has to reach a colour space converter rules out the direct routes, which do
        // nothing but relayout their samples. Selecting the route also builds what it needs to run.
        bool requiresColorConversion = (_stages & PaletteFoldedStages) != 0 || RequiresColorTransform(parameters);

        if (parameters.HasImageMask)
        {
            if (bitsPerComponent == 1 && _components == 1 && (_stages & AlphaStages) == 0)
            {
                _pipeline = RowPipeline.DirectGray;

                if (StencilPaintsOnZero(parameters))
                {
                    _stages |= RowStages.Invert;
                }
            }
            else
            {
                _pipeline = RowPipeline.Palette;
                _indexedPalette = BuildStencilMaskPalette(parameters);
            }
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

        if (_pipeline == RowPipeline.Palette)
        {
            // The palette has already produced 8-bit RGBA, so the resampler reads that instead of samples.
            _indexedBitsPerComponent = bitsPerComponent;
            _resampleComponents = 4;
            _resampleSourceBitsPerComponent = NormalizedBitsPerComponent;
        }
        else
        {
            _resampleComponents = _components + (((_stages & RowStages.AlphaInterleaved) != 0) ? 1 : 0);
            _resampleSourceBitsPerComponent = bitsPerComponent;
        }

        // The palette route merges its alpha before resampling, so only the other routes need a
        // second converter to bring the alpha plane to the output grid in lockstep with the color.
        _needsAlphaConverter = (_stages & RowStages.AlphaPlane) != 0 && _pipeline != RowPipeline.Palette;

        // A stencil carries no color of its own: the fill color arrives with the paint it is drawn with.
        if (parameters.HasImageMask && _pipeline != RowPipeline.Palette)
        {
            _colorFormat = PdfImageColorFormat.Alpha;
            _alphaType = PdfImageAlphaType.Unpremultiplied;
        }
        // A single gray channel with no alpha stage to run is the only case that stays out of RGBA.
        else if (_pipeline == RowPipeline.DirectGray && (_stages & AlphaStages) == 0)
        {
            _colorFormat = PdfImageColorFormat.Gray;
            _alphaType = PdfImageAlphaType.Opaque;
        }
        else
        {
            // Colour key masking and the folded palette write zero alpha into buffers with no alpha source.
            _colorFormat = PdfImageColorFormat.Rgba;
            _alphaType = PdfImageAlphaType.Unpremultiplied;

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
            _indexedPalette = FoldDecodeAndMaskIntoPalette(_indexedPalette);
            _stages &= ~PaletteFoldedStages;
        }

        bool writesConverterOutputDirectly = _pipeline == RowPipeline.Palette
            || (_pipeline == RowPipeline.DirectGray && (_stages & AlphaStages) == 0)
            || (_pipeline == RowPipeline.DirectRgb && (_stages & RowStages.AlphaInterleaved) != 0);

        if (_pipeline == RowPipeline.Palette && parameters.DownscaledSize.HasValue)
        {
            _paletteRgbaBuffer = new byte[sourceWidth * 4];
        }

        if (!writesConverterOutputDirectly)
        {
            _convertedRowBuffer = new byte[(sourceWidth * _resampleComponents) + 1];
        }

        if (_needsAlphaConverter)
        {
            _convertedAlphaBuffer = new byte[sourceWidth];
        }
    }

    /// <summary>
    /// Creates a destination covering <paramref name="sourceWidth"/> pixels from
    /// <paramref name="sourceStart"/> of every source row, over <paramref name="sourceHeight"/> rows.
    /// </summary>
    public PdfImageRowTarget CreateTarget(int sourceStart, int sourceWidth, int sourceHeight, PdfIntegerSize? downscaledSize)
    {
        int outputWidth = downscaledSize?.Width ?? sourceWidth;
        int outputHeight = downscaledSize?.Height ?? sourceHeight;

        IRowConverter? colorConverter;
        IRowConverter? alphaConverter = null;

        if (downscaledSize.HasValue)
        {
            colorConverter = new AveragingDownsampleRowConverter(_resampleComponents, _resampleSourceBitsPerComponent, sourceWidth, outputWidth, sourceHeight, outputHeight);

            if (_needsAlphaConverter)
            {
                alphaConverter = new AveragingDownsampleRowConverter(1, NormalizedBitsPerComponent, sourceWidth, outputWidth, sourceHeight, outputHeight);
            }
        }
        else if (_pipeline == RowPipeline.Palette)
        {
            colorConverter = null;
        }
        else
        {
            colorConverter = new SampleNormalizingRowConverter(_resampleComponents, _resampleSourceBitsPerComponent, outputWidth);

            if (_needsAlphaConverter)
            {
                alphaConverter = new SampleNormalizingRowConverter(1, NormalizedBitsPerComponent, outputWidth);
            }
        }

        PdfDecodedImage image = new(outputWidth, outputHeight, _colorFormat, _alphaType);

        return new PdfImageRowTarget(sourceStart, sourceWidth, outputWidth, colorConverter, alphaConverter, image);
    }

    /// <summary>
    /// Writes one decoded source row into every non-null target in <paramref name="targets"/>,
    /// each taking its own region of the row.
    /// </summary>
    /// <param name="rowIndex">Index of this row within the targets' shared source extent.</param>
    /// <param name="sourceRow">Packed samples of the full source row.</param>
    /// <param name="alphaRow">Full-width alpha plane for this row, or empty when there is none.</param>
    /// <param name="targets">Destinations to fill, in source order.</param>
    /// <param name="observer">Observer notified after each target, or null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecodeRow(
        int rowIndex,
        in ReadOnlySpan<byte> sourceRow,
        in ReadOnlySpan<byte> alphaRow,
        PdfImageRowTarget?[] targets,
        IPdfExecutionObserver? observer)
    {
        foreach (PdfImageRowTarget? target in targets)
        {
            if (target == null || !target.HasRoomForRow)
            {
                continue;
            }

            ReadOnlySpan<byte> targetAlphaRow = (alphaRow.IsEmpty)
                ? default
                : alphaRow.Slice(target.SourceStart, target.SourceWidth);

            switch (_pipeline)
            {
                case RowPipeline.Palette:
                {
                    DecodePaletteRow(rowIndex, sourceRow, targetAlphaRow, target);
                    break;
                }
                case RowPipeline.Transformed:
                {
                    DecodeTransformedRow(rowIndex, sourceRow, targetAlphaRow, target);
                    break;
                }
                case RowPipeline.DirectGray:
                {
                    DecodeDirectGrayRow(rowIndex, sourceRow, targetAlphaRow, target);
                    break;
                }
                case RowPipeline.DirectRgb:
                {
                    DecodeDirectRgbRow(rowIndex, sourceRow, targetAlphaRow, target);
                    break;
                }
            }

            observer?.Notify();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetSourceStartBit(PdfImageRowTarget target) => target.SourceStart * _sourceBitsPerPixel;
}
