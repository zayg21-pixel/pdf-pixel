using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class SoftMaskImageExecutionContext : IDisposable
{
    public SoftMaskImageExecutionContext(
        SKSizeI imageSize,
        SKSizeI maskSize,
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

    public SKSizeI ImageSize { get; }
    public SKSizeI MaskSize { get; }
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
            throw new ArgumentException($"Not defined soft mask for image {pdfImage.Name}.");
        }

        PdfImage maskImage = pdfImage.SoftMask;
        SKSizeI imageSize = new(pdfImage.Width, pdfImage.Height);
        SKSizeI maskSize = new(maskImage.Width, maskImage.Height);

        PdfImageDecoder? imageDecoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        PdfImageDecoder? maskDecoder = PdfImageDecoder.GetDecoder(maskImage, loggerFactory);

        if (imageDecoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        if (maskDecoder == null)
        {
            throw new ArgumentException($"Mask decoder for image {maskImage.Type} is not defined.");
        }

        if (maskSize != imageSize)
        {
            throw new ArgumentException($"Image size {imageSize} is not equal to mask size {maskSize}.");
        }

        PdfTileInfo tileInfo = new(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));

        return new SoftMaskImageExecutionContext(
            imageSize,
            maskSize,
            context,
            new PdfImageTileCacheEntry(imageDecoder, context, tileInfo),
            new PdfImageTileCacheEntry(maskDecoder, context, tileInfo),
            maskImage.MatteArray,
            pdfImage.Interpolate);
    }

    public void Dispose()
    {
        ImageCache.Dispose();
        MaskCache.Dispose();
    }
}

