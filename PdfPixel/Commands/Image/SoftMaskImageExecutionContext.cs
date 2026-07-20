using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class SoftMaskImageExecutionContext : IDisposable
{
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

    public PdfIntegerSize ImageSize { get; }
    public PdfIntegerSize MaskSize { get; }
    public ImageDecodingContext DecodingContext { get; }
    public PdfImageTileCacheEntry ImageCache { get; }
    public PdfImageTileCacheEntry MaskCache { get; }
    public float[]? MatteArray { get; }
    public bool Interpolate { get; }
    public PdfTileInfo TileInfo => ImageCache.TileInfo;

    public static SoftMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
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
        bool maskMatchesImageSize = maskSize.Equals(imageSize);

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

    public void Dispose()
    {
        ImageCache.Dispose();
        MaskCache.Dispose();
    }
}

