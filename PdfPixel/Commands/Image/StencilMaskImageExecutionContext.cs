using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using System;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Constructed context for a <see cref="DrawStencilMaskImageTileCommand"/>: a 1-bit stencil mask blended with the current fill color, drawn tile by tile.
/// </summary>
public sealed class StencilMaskImageExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StencilMaskImageExecutionContext"/> class.
    /// </summary>
    public StencilMaskImageExecutionContext(in PdfIntegerSize imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache, bool invertMask, bool interpolate)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
        InvertMask = invertMask;
        Interpolate = interpolate;
    }

    /// <summary>
    /// Gets the image's pixel size.
    /// </summary>
    public PdfIntegerSize ImageSize { get; }

    /// <summary>
    /// Gets the decoding context (fill color, alpha, blend mode) captured at record time.
    /// </summary>
    public ImageDecodingContext DecodingContext { get; }

    /// <summary>
    /// Gets the tile cache producing decoded tiles for this image.
    /// </summary>
    public PdfImageTileCacheEntry TileCache { get; }

    /// <summary>
    /// Gets the tiling layout of <see cref="TileCache"/>.
    /// </summary>
    public PdfTileInfo TileInfo => TileCache.TileInfo;

    /// <summary>
    /// Whether the stencil mask should be inverted when compositing.
    /// False when the image Decode array is [1 0], true otherwise (default [0 1] behavior).
    /// </summary>
    public bool InvertMask { get; }

    /// <summary>
    /// Gets whether the image should be interpolated when scaled.
    /// </summary>
    public bool Interpolate { get; }

    /// <summary>
    /// Builds the context for <paramref name="pdfImage"/>, resolving its decoder, tile cache, and invert-mask flag.
    /// </summary>
    public static StencilMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        if (pdfImage == null)
        {
            throw new ArgumentNullException(nameof(pdfImage));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        PdfIntegerSize imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, context, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        PdfRange[]? decode = pdfImage.Decode;
        bool invertMask = decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;

        PdfTileInfo tileInfo = new(imageSize, new PdfIntegerSize(context.DefaultTileSize, context.DefaultTileSize));
        return new StencilMaskImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, tileInfo), invertMask, pdfImage.Interpolate);
    }
}
