using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Rendering;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Immutable snapshot of the graphics state values needed for image decoding, resolved for a specific
/// <see cref="PdfImage"/>. Captured at command creation time so the command can later re-decode with
/// an updated <see cref="PdfCommandExecutionParameters"/> at execution time.
/// </summary>
internal sealed class ImageDecodingContext : IEquatable<ImageDecodingContext>
{
    /// <summary>
    /// Creates a decoding context by capturing the relevant values from a <see cref="PdfGraphicsState"/>
    /// and resolving <paramref name="image"/>'s color space against the state's content host.
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

        Host = state.Host;
        ColorSpaceConverter = ResolveColorSpaceConverter(image);
        DefaultTileSize = state.RenderingParameters.ImageTileSize;
        CacheDecodedTiles = state.RenderingParameters.CacheDecodedTiles;
        TransferFunction = state.TransferFunction;
        UseBlackPointCompensation = state.UseBlackPointCompensation;
        FillPaint = state.FillPaint;
        ApplyMask = true;
    }

    /// <summary>
    /// Creates a context for decoding <paramref name="image"/> on its own, as a single tile, resolving its
    /// color space against <paramref name="host"/> with the values of <paramref name="parameters"/>.
    /// </summary>
    public ImageDecodingContext(PdfImage image, IPdfContentHost host, PdfImageDecodingParameters parameters)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        Host = host ?? throw new ArgumentNullException(nameof(host));
        ColorSpaceConverter = ResolveColorSpaceConverter(image);
        DefaultTileSize = Math.Max(image.Width, image.Height);
        CacheDecodedTiles = false;
        TransferFunction = null;
        UseBlackPointCompensation = parameters.UseBlackPointCompensation;
        FillPaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Fill);
        ApplyMask = parameters.ApplyMask;
    }

    /// <summary>
    /// Creates a context derived from <paramref name="source"/> but resolved for <paramref name="image"/>
    /// and with explicit compositing overrides. Used for cases such as pattern-layer masking, where the
    /// target image and the desired blend mode and fill color differ from the original graphics state.
    /// </summary>
    /// <param name="source">The context to derive shared values (content host, tile sizing, transfer function) from.</param>
    /// <param name="image">The image this context resolves a color space converter for.</param>
    /// <param name="fillPaint">
    /// Fill paint override. A mask paint (<see cref="PdfPaint.MaskParameters"/>) draws the image as the mask of what is
    /// already painted beneath it.
    /// </param>
    public ImageDecodingContext(ImageDecodingContext source, PdfImage image, PdfPaint fillPaint)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        Host = source.Host;
        ColorSpaceConverter = ResolveColorSpaceConverter(image);
        DefaultTileSize = source.DefaultTileSize;
        CacheDecodedTiles = source.CacheDecodedTiles;
        TransferFunction = source.TransferFunction;
        UseBlackPointCompensation = source.UseBlackPointCompensation;
        FillPaint = fillPaint ?? throw new ArgumentNullException(nameof(fillPaint));
        ApplyMask = source.ApplyMask;
    }

    /// <summary>
    /// Content host the image was drawn in. Some color space converters (e.g. Separation, Indexed) need
    /// host-level resource resolution to build their underlying converters.
    /// </summary>
    internal IPdfContentHost Host { get; }

    /// <summary>
    /// Color space converter resolved for the target image's /ColorSpace entry. Null when not declared.
    /// </summary>
    public PdfColorSpaceConverter? ColorSpaceConverter { get; }

    /// <summary>
    /// Default tile size.
    /// </summary>
    public int DefaultTileSize { get; }

    /// <summary>
    /// When true, decoded tiles are retained across renders for the tiles inside the drawn region.
    /// When false, only the tile row currently being drawn is held.
    /// </summary>
    public bool CacheDecodedTiles { get; }

    /// <summary>
    /// Transfer function (TR) from the graphics state, applied during color conversion.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// Whether black point compensation from the graphics state is applied during color conversion.
    /// </summary>
    public bool UseBlackPointCompensation { get; }

    /// <summary>
    /// Fill paint from the graphics state. Its color is used for stencil mask rendering, and its blend
    /// mode is superseded by destination-in compositing when it is a mask paint (<see cref="PdfPaint.MaskParameters"/>).
    /// </summary>
    public PdfPaint FillPaint { get; }

    /// <summary>
    /// Whether the image's mask (/SMask, /SMaskInData or /Mask) is applied as alpha.
    /// </summary>
    public bool ApplyMask { get; }

    /// <summary>
    /// Returns whether <paramref name="other"/> decodes an image into the same tiles: the same host, color space converter,
    /// transfer function and fill paint instances, and the same tiling and masking settings.
    /// </summary>
    public bool Equals(ImageDecodingContext? other)
    {
        if (other == null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ReferenceEquals(Host, other.Host)
            && ReferenceEquals(ColorSpaceConverter, other.ColorSpaceConverter)
            && ReferenceEquals(TransferFunction, other.TransferFunction)
            && ReferenceEquals(FillPaint, other.FillPaint)
            && DefaultTileSize == other.DefaultTileSize
            && CacheDecodedTiles == other.CacheDecodedTiles
            && UseBlackPointCompensation == other.UseBlackPointCompensation
            && ApplyMask == other.ApplyMask;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ImageDecodingContext);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(RuntimeHelpers.GetHashCode(Host), RuntimeHelpers.GetHashCode(FillPaint), DefaultTileSize, ApplyMask);

    private PdfColorSpaceConverter? ResolveColorSpaceConverter(PdfImage image) => Host.Resources.ResolveColorSpace(image.ColorSpaceReference, defaultComponents: -1);
}
