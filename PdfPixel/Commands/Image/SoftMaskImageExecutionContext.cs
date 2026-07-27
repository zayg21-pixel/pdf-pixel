using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using System;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Constructed context for a <see cref="DrawSoftMaskImageTileCommand"/>: an image blended through a soft mask (/SMask), drawn tile by tile.
/// </summary>
public sealed class SoftMaskImageExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftMaskImageExecutionContext"/> class.
    /// </summary>
    public SoftMaskImageExecutionContext(
        in PdfIntegerSize imageSize,
        in PdfIntegerSize maskSize,
        ImageDecodingContext decodingContext,
        PdfImageTileCacheEntry imageCache,
        PdfImageTileCacheEntry maskCache,
        float[]? matteArray,
        bool interpolate)
    {
        ImageSize = imageSize;
        MaskSize = maskSize;
        DecodingContext = decodingContext;
        ImageCache = imageCache;
        MaskCache = maskCache;
        MatteArray = matteArray;
        Interpolate = interpolate;
    }

    /// <summary>
    /// Gets the image's pixel size.
    /// </summary>
    public PdfIntegerSize ImageSize { get; }

    /// <summary>
    /// Gets the soft mask's pixel size.
    /// </summary>
    public PdfIntegerSize MaskSize { get; }

    /// <summary>
    /// Gets the decoding context (fill color, alpha, blend mode) captured at record time.
    /// </summary>
    public ImageDecodingContext DecodingContext { get; }

    /// <summary>
    /// Gets the tile cache producing decoded tiles for the image.
    /// </summary>
    public PdfImageTileCacheEntry ImageCache { get; }

    /// <summary>
    /// Gets the tile cache producing decoded tiles for the soft mask.
    /// </summary>
    public PdfImageTileCacheEntry MaskCache { get; }

    /// <summary>
    /// Gets the soft mask's /Matte backdrop color components, or null when not specified.
    /// </summary>
    public float[]? MatteArray { get; }

    /// <summary>
    /// Gets whether the image should be interpolated when scaled.
    /// </summary>
    public bool Interpolate { get; }

    /// <summary>
    /// Gets the tiling layout of <see cref="ImageCache"/>.
    /// </summary>
    public PdfTileInfo TileInfo => ImageCache.TileInfo;

    /// <summary>
    /// Builds the context for <paramref name="pdfImage"/> and its soft mask, resolving their decoders and tile caches.
    /// </summary>
    public static SoftMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        if (pdfImage == null)
        {
            throw new ArgumentNullException(nameof(pdfImage));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (pdfImage.SoftMask == null)
        {
            throw new ArgumentException($"Not defined soft mask for image {pdfImage.SourceReference}.");
        }

        PdfImage maskImage = pdfImage.SoftMask;
        PdfIntegerSize imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfIntegerSize maskSize = new(maskImage.Width, maskImage.Height);

        ImageDecodingContext maskDecodingContext = new(context, maskImage, context.FillColor, context.FillAlpha, context.BlendMode);

        PdfImageDecoder? imageDecoder = PdfImageDecoder.GetDecoder(pdfImage, context, loggerFactory);
        PdfImageDecoder? maskDecoder = PdfImageDecoder.GetDecoder(maskImage, maskDecodingContext, loggerFactory);

        if (imageDecoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        if (maskDecoder == null)
        {
            throw new ArgumentException($"Mask decoder for image {maskImage.Type} is not defined.");
        }

        PdfIntegerSize defaultTileSize = new(context.DefaultTileSize, context.DefaultTileSize);
        bool maskMatchesImageSize = maskSize == imageSize;

        PdfTileInfo imageTileInfo = maskMatchesImageSize ? new PdfTileInfo(imageSize, defaultTileSize) : new PdfTileInfo(imageSize, imageSize);
        PdfTileInfo maskTileInfo = maskMatchesImageSize ? new PdfTileInfo(maskSize, defaultTileSize) : new PdfTileInfo(maskSize, maskSize);

        return new SoftMaskImageExecutionContext(
            imageSize,
            maskSize,
            context,
            new PdfImageTileCacheEntry(imageDecoder, imageTileInfo),
            new PdfImageTileCacheEntry(maskDecoder, maskTileInfo),
            maskImage.MatteArray,
            pdfImage.Interpolate);
    }
}
