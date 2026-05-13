using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskImageExecutionContext : IDisposable
{
    public SKSizeI ImageSize { get; private set; }
    public ImageDecodingContext DecodingContext { get; private set; }
    public PdfImageTileCacheEntry TileCache { get; private set; }
    public PdfTileInfo TileInfo => TileCache.TileInfo;

    public static StencilMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        var imageSize = new SKSizeI(pdfImage.Width, pdfImage.Height);
        var decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var tileInfo = new PdfTileInfo(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        return new StencilMaskImageExecutionContext
        {
            ImageSize = imageSize,
            DecodingContext = context,
            TileCache = new PdfImageTileCacheEntry(decoder, context, tileInfo)
        };
    }

    public void Dispose() => TileCache.Dispose();
}
