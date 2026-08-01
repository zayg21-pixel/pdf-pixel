using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
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
        BlendMode = state.FillPaint.BlendMode;
    }

    /// <summary>
    /// Creates a context derived from <paramref name="source"/> but resolved for <paramref name="image"/>
    /// and with explicit compositing overrides. Used for cases such as pattern-layer masking, where the
    /// target image and the desired blend mode and fill colour differ from the original graphics state.
    /// </summary>
    /// <param name="source">The context to derive shared values (page, tile sizing, transfer function) from.</param>
    /// <param name="image">The image this context resolves a color space converter for.</param>
    /// <param name="fillColor">Fill color override.</param>
    /// <param name="fillAlpha">Fill alpha override.</param>
    /// <param name="blendMode">Blend mode override.</param>
    /// <param name="isStencilMaskComposite">
    /// True to composite via destination-in (Porter-Duff) regardless of <paramref name="blendMode"/>, for
    /// the internal stencil-mask alpha application pass. Has no PDF spec equivalent.
    /// </param>
    public ImageDecodingContext(ImageDecodingContext source, PdfImage image, in PdfColor fillColor, float fillAlpha, PdfBlendMode blendMode, bool isStencilMaskComposite)
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
        IsStencilMaskComposite = isStencilMaskComposite;
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
    /// Blend mode for paint composition; the PDF content blend mode from the graphics state.
    /// Superseded by destination-in compositing when <see cref="IsStencilMaskComposite"/> is true.
    /// </summary>
    public PdfBlendMode BlendMode { get; }

    /// <summary>
    /// True when this context is for the internal stencil-mask alpha application pass, which composites
    /// via destination-in (Porter-Duff) regardless of <see cref="BlendMode"/>. Has no PDF spec equivalent.
    /// </summary>
    public bool IsStencilMaskComposite { get; }

    private PdfColorSpaceConverter? ResolveColorSpaceConverter(PdfImage image) => Page.Cache.ColorSpace.ResolveByObject(image.ColorSpaceObject, defaultComponents: -1);
}
