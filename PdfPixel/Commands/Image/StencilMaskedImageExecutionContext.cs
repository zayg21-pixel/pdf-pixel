using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskedImageExecutionContext : IDisposable
{
    public StencilMaskedImageExecutionContext(SKSizeI imageSize, SKSizeI maskSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry imageCache, PdfImageTileCacheEntry maskCache, bool invertMask)
    {
        ImageSize = imageSize;
        MaskSize = maskSize;
        DecodingContext = decodingContext;
        ImageCache = imageCache;
        MaskCache = maskCache;
        InvertMask = invertMask;
    }

    public SKSizeI ImageSize { get; }

    public SKSizeI MaskSize { get;}

    public ImageDecodingContext DecodingContext { get; }

    public PdfImageTileCacheEntry ImageCache { get; }

    public PdfImageTileCacheEntry MaskCache { get; }

    public PdfTileInfo TileInfo => ImageCache.TileInfo;

    /// <summary>
    /// Whether the stencil mask should be inverted when compositing.
    /// False when the mask Decode array is [1 0], true otherwise (default [0 1] behavior).
    /// </summary>
    public bool InvertMask { get; }

    public static StencilMaskedImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        if (pdfImage.StencilMask == null)
        {
            throw new ArgumentException($"Not defined soft mask for image {pdfImage.Name}.");
        }

        PdfImage maskImage = pdfImage.StencilMask;

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

        (PdfTileInfo imageTileInfo, PdfTileInfo maskTileInfo) = PdfImageCommandUtilities.ComputePairedTileSizes(pdfImage, maskImage, context.DefaultTileSize);

        float[]? maskDecode = maskImage.DecodeArray;
        bool invertMask = maskDecode == null || maskDecode.Length < 2 || maskDecode[0] < maskDecode[1];

        return new StencilMaskedImageExecutionContext(
            imageSize,
            maskSize,
            context,
            new PdfImageTileCacheEntry(imageDecoder, context, imageTileInfo),
            new PdfImageTileCacheEntry(maskDecoder, context, maskTileInfo),
            invertMask);
    }

    public void Dispose()
    {
        ImageCache.Dispose();
        MaskCache.Dispose();
    }
}
