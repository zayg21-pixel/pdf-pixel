using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Immutable snapshot of the graphics state values needed for image decoding.
/// Captured at command creation time so the command can later re-decode with
/// an updated <see cref="PdfCommandExecutionParameters"/> at execution time.
/// </summary>
public sealed class ImageDecodingContext
{
    /// <summary>
    /// Creates a decoding context by capturing the relevant values from a <see cref="PdfGraphicsState"/>.
    /// </summary>
    public ImageDecodingContext(PdfGraphicsState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        DefaultTileSize = state.RenderingParameters.ImageTileSize;
        MaxTileCacheSizeBytes = state.RenderingParameters.MaxTileCacheSizeBytes;
        FullTransferFunction = state.FullTransferFunction;
        FillColor = state.FillPaint.Color;
        FillAlpha = state.FillAlpha;
        BlendMode = PdfBlendModeNames.ToSkiaBlendMode(state.BlendMode);
    }

    /// <summary>
    /// Creates a context derived from <paramref name="source"/> but with explicit compositing overrides.
    /// Used for cases such as pattern-layer masking, where the desired blend mode and fill colour
    /// differ from the original graphics state.
    /// </summary>
    public ImageDecodingContext(ImageDecodingContext source, in SKColor fillColor, float fillAlpha, SKBlendMode blendMode)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        DefaultTileSize = source.DefaultTileSize;
        MaxTileCacheSizeBytes = source.MaxTileCacheSizeBytes;
        FullTransferFunction = source.FullTransferFunction;
        FillColor = fillColor;
        FillAlpha = fillAlpha;
        BlendMode = blendMode;
    }

    /// <summary>
    /// Default tile size.
    /// </summary>
    public int DefaultTileSize { get; }

    /// <summary>
    /// Upper bound on the combined estimated byte size of cached decoded tiles.
    /// </summary>
    public long MaxTileCacheSizeBytes { get; }

    /// <summary>
    /// Combined transfer function (internal + external) for color conversion.
    /// </summary>
    public IColorTransform? FullTransferFunction { get; }

    /// <summary>
    /// Fill color from the graphics state, used for stencil mask rendering.
    /// </summary>
    public SKColor FillColor { get; }

    /// <summary>
    /// Image fill alpha.
    /// </summary>
    public float FillAlpha { get; }

    /// <summary>
    /// Skia blend mode for paint composition, converted from the PDF blend mode at construction.
    /// </summary>
    public SKBlendMode BlendMode { get; }
}
