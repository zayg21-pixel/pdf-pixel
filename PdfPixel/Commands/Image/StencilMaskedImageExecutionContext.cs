using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskedImageExecutionContext : IDisposable
{
    public SKSizeI ImageSize { get; private set; }
    public SKSizeI MaskSize { get; private set; }
    public ImageDecodingContext DecodingContext { get; private set; }
    public PdfImageTileCacheEntry ImageCache { get; private set; }
    public PdfImageTileCacheEntry MaskCache { get; private set; }
    public PdfTileInfo TileInfo => ImageCache.TileInfo;

    public static StencilMaskedImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        var maskImage = pdfImage.StencilMask;
        var imageSize = new SKSizeI(pdfImage.Width, pdfImage.Height);
        var maskSize = new SKSizeI(maskImage.Width, maskImage.Height);

        var imageDecoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var maskDecoder = PdfImageDecoder.GetDecoder(maskImage, loggerFactory);

        var (imageTileInfo, maskTileInfo) = PdfImageCommandUtilities.ComputePairedTileSizes(pdfImage, maskImage, context.DefaultTileSize);

        return new StencilMaskedImageExecutionContext
        {
            ImageSize = imageSize,
            MaskSize = maskSize,
            DecodingContext = context,
            ImageCache = new PdfImageTileCacheEntry(imageDecoder, context, imageTileInfo),
            MaskCache = new PdfImageTileCacheEntry(maskDecoder, context, maskTileInfo)
        };
    }

    public void Dispose()
    {
        ImageCache.Dispose();
        MaskCache.Dispose();
    }
}
