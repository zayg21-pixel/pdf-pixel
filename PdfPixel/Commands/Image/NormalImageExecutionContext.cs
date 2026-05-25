using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class NormalImageExecutionContext : IDisposable
{
    public SKSizeI ImageSize { get; private set; }
    public ImageDecodingContext DecodingContext { get; private set; }
    public PdfImageTileCacheEntry TileCache { get; private set; }
    public bool Interpolate { get; private set; }
    public PdfTileInfo TileInfo => TileCache.TileInfo;

    public static NormalImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        SKSizeI imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        PdfTileInfo tileInfo = new(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        return new NormalImageExecutionContext
        {
            ImageSize = imageSize,
            DecodingContext = context,
            TileCache = new PdfImageTileCacheEntry(decoder, context, tileInfo),
            Interpolate = pdfImage.Interpolate
        };
    }

    public void Dispose() => TileCache.Dispose();
}
