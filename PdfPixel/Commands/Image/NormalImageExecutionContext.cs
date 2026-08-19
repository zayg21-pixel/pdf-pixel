using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using System;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Constructed context for a <see cref="DrawNormalImageTileCommand"/>: an image with no mask, drawn tile by tile.
/// </summary>
public sealed class NormalImageExecutionContext
{
    private NormalImageExecutionContext(in PdfIntegerSize imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache, bool interpolate)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
        Interpolate = interpolate;
    }

    /// <summary>
    /// Builds the context for <paramref name="pdfImage"/>, resolving its decoder and tile cache.
    /// </summary>
    public static NormalImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
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

        SoftMaskAlphaRowSource? alphaRowSource = CreateAlphaRowSource(pdfImage, context, loggerFactory);

        PdfTileInfo tileInfo = new(imageSize, new PdfIntegerSize(context.DefaultTileSize, context.DefaultTileSize));
        return new NormalImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, tileInfo, loggerFactory, alphaRowSource), pdfImage.Interpolate);
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
    /// Gets whether the image should be interpolated when scaled.
    /// </summary>
    public bool Interpolate { get; }

    /// <summary>
    /// Gets the tiling layout of <see cref="TileCache"/>.
    /// </summary>
    public PdfTileInfo TileInfo => TileCache.TileInfo;

    private static SoftMaskAlphaRowSource? CreateAlphaRowSource(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        PdfImage? softMask = pdfImage.SoftMask;
        if (softMask == null)
        {
            return null;
        }

        ImageDecodingContext maskContext = new(context, softMask, context.FillPaint, isStencilMaskComposite: false);
        PdfImageDecoder? maskDecoder = PdfImageDecoder.GetDecoder(softMask, maskContext, loggerFactory);

        if (maskDecoder == null)
        {
            throw new ArgumentException($"Mask decoder for image {softMask.Type} is not defined.");
        }

        return new SoftMaskAlphaRowSource(maskDecoder, loggerFactory);
    }
}
