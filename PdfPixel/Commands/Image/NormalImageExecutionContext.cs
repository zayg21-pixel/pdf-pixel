using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class NormalImageExecutionContext : IDisposable
{
    private NormalImageExecutionContext(SKSizeI imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache, bool interpolate)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
        Interpolate = interpolate;
    }

    public static NormalImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        SKSizeI imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, context, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        PdfTileInfo tileInfo = new(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        return new NormalImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, tileInfo), pdfImage.Interpolate);
    }

    public SKSizeI ImageSize { get; }

    public ImageDecodingContext DecodingContext { get; }

    public PdfImageTileCacheEntry TileCache { get; }

    public bool Interpolate { get; }

    public PdfTileInfo TileInfo => TileCache.TileInfo;

    public void Dispose() => TileCache.Dispose();
}
