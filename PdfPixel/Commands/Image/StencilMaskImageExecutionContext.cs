using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskImageExecutionContext : IDisposable
{
    public StencilMaskImageExecutionContext(SKSizeI imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
    }

    public SKSizeI ImageSize { get;}

    public ImageDecodingContext DecodingContext { get;}

    public PdfImageTileCacheEntry TileCache { get; }

    public PdfTileInfo TileInfo => TileCache.TileInfo;

    public static StencilMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        SKSizeI imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        PdfTileInfo tileInfo = new(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        return new StencilMaskImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, context, tileInfo));
    }

    public void Dispose() => TileCache.Dispose();
}
