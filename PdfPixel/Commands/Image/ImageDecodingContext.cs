using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Immutable snapshot of the graphics state values needed for image decoding, resolved for a specific
/// <see cref="PdfImage"/>. Captured at command creation time so the command can later re-decode with
/// an updated <see cref="PdfCommandExecutionParameters"/> at execution time.
/// </summary>
public sealed class ImageDecodingContext
{
    /// <summary>
    /// Creates a decoding context by capturing the relevant values from a <see cref="PdfGraphicsState"/>
    /// and resolving <paramref name="image"/>'s color space against the state's page.
    /// </summary>
    public ImageDecodingContext(PdfImage image, PdfGraphicsState state)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        Page = state.Page;
        ColorSpaceConverter = ResolveColorSpaceConverter(image);
        DefaultTileSize = state.RenderingParameters.ImageTileSize;
        MaxTileCacheSizeBytes = state.RenderingParameters.MaxTileCacheSizeBytes;
        FullTransferFunction = state.FullTransferFunction;
        FillColor = state.FillPaint.Color;
        FillAlpha = state.FillPaint.Alpha;
        BlendMode = SkiaEnumUtilities.ToSkiaBlendMode(state.FillPaint.BlendMode);
    }

    /// <summary>
    /// Creates a context derived from <paramref name="source"/> but resolved for <paramref name="image"/>
    /// and with explicit compositing overrides. Used for cases such as pattern-layer masking, where the
    /// target image and the desired blend mode and fill colour differ from the original graphics state.
    /// </summary>
    public ImageDecodingContext(ImageDecodingContext source, PdfImage image, in PdfColor fillColor, float fillAlpha, SKBlendMode blendMode)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        Page = source.Page;
        ColorSpaceConverter = ResolveColorSpaceConverter(image);
        DefaultTileSize = source.DefaultTileSize;
        MaxTileCacheSizeBytes = source.MaxTileCacheSizeBytes;
        FullTransferFunction = source.FullTransferFunction;
        FillColor = fillColor;
        FillAlpha = fillAlpha;
        BlendMode = blendMode;
    }

    /// <summary>
    /// Page the image was drawn on. Some color space converters (e.g. Separation, Indexed) need
    /// page-level resource resolution to build their underlying converters.
    /// </summary>
    internal IPdfPageInternal Page { get; }

    /// <summary>
    /// Color space converter resolved for the target image's /ColorSpace entry. Null when not declared.
    /// </summary>
    public PdfColorSpaceConverter? ColorSpaceConverter { get; }

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
    public PdfColor FillColor { get; }

    /// <summary>
    /// Image fill alpha.
    /// </summary>
    public float FillAlpha { get; }

    /// <summary>
    /// Skia blend mode for paint composition, converted from the PDF blend mode at construction.
    /// </summary>
    public SKBlendMode BlendMode { get; }

    private PdfColorSpaceConverter? ResolveColorSpaceConverter(PdfImage image) => Page.Cache.ColorSpace.ResolveByObject(image.ColorSpaceObject, defaultComponents: -1);
}
