using SkiaSharp;
using PdfReader.Models;
using PdfReader.Color.ColorSpace;
using PdfReader.Pattern.Utilities;
using PdfReader.Rendering.State;
using PdfReader.Rendering;

namespace PdfReader.Pattern.Model;

/// <summary>
/// Strongly typed paint type for tiling patterns (PDF spec Table 90)
/// </summary>
public enum PdfTilingPaintType
{
    Colored = 1,
    Uncolored = 2
}

/// <summary>
/// Strongly typed tiling type (PDF spec Table 91)
/// </summary>
public enum PdfTilingSpacingType
{
    ConstantSpacing = 1,
    NoDistortion = 2,
    ConstantSpacingFast = 3
}

/// <summary>
/// Represents a parsed tiling (/PatternType 1) pattern.
/// </summary>
public sealed class PdfTilingPattern : PdfPattern
{
    private SKPicture _cachedBasePicture;
    private readonly IPdfRenderer _renderer;

    internal PdfTilingPattern(
        IPdfRenderer renderer,
        PdfPage page,
        PdfObject sourceObject,
        SKRect bbox,
        float xStep,
        float yStep,
        PdfTilingPaintType paintTypeKind,
        PdfTilingSpacingType tilingTypeKind,
        SKMatrix matrix)
        : base(page, sourceObject, matrix, PdfPatternType.Tiling)
    {
        _renderer = renderer;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        PaintTypeKind = paintTypeKind;
        TilingTypeKind = tilingTypeKind;
    }

    /// <summary>
    /// Gets the bounding box of the pattern cell.
    /// </summary>
    public SKRect BBox { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the paint type (colored or uncolored).
    /// </summary>
    public PdfTilingPaintType PaintTypeKind { get; }

    /// <summary>
    /// Gets the tiling type (spacing and distortion rules).
    /// </summary>
    public PdfTilingSpacingType TilingTypeKind { get; }

    /// <inheritdoc/>
    public override SKPicture AsPicture(PdfGraphicsState state)
    {
        if (_cachedBasePicture == null)
        {
            _cachedBasePicture = TilingPatternShaderBuilder.ToBaseShader(_renderer, this, Page);
        }

        if (_cachedBasePicture == null)
        {
            return null;
        }

        if (PaintTypeKind == PdfTilingPaintType.Uncolored)
        {
            if (state.FillPaint != null && state.FillPaint.PatternComponents != null)
            {
                var patternColorSpace = state.FillColorConverter as PatternColorSpaceConverter;

                if (patternColorSpace != null && patternColorSpace.BaseColorSpace != null)
                {
                    var tintedPicture = new SKPictureRecorder();
                    var canvas = tintedPicture.BeginRecording(_cachedBasePicture.CullRect);
                    var tintPaint = new SKPaint // TODO: move to paints!
                    {
                        IsAntialias = true,
                        Color = SKColors.Red,
                        ColorFilter = SKColorFilter.CreateBlendMode(patternColorSpace.BaseColorSpace.ToSrgb(state.FillPaint.PatternComponents, state.RenderingIntent), SKBlendMode.SrcIn)

                    };

                    canvas.DrawPicture(_cachedBasePicture, tintPaint);
                    return tintedPicture.EndRecording();

                    //SKColor tintColor = patternColorSpace.BaseColorSpace.ToSrgb(state.FillPaint.PatternComponents, state.RenderingIntent);
                    //var tintColorFilter = SKColorFilter.CreateBlendMode(tintColor, SKBlendMode.SrcIn);
                    //return transformedShader.WithColorFilter(tintColorFilter);
                }
            }
        }

        return _cachedBasePicture;
    }

    /// <summary>
    /// Disposes the cached shader and releases resources.
    /// </summary>
    public override void Dispose()
    {
        if (_cachedBasePicture != null)
        {
            _cachedBasePicture.Dispose();
            _cachedBasePicture = null;
        }

        base.Dispose();
    }
}
