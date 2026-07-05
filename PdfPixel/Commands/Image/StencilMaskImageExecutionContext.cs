using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskImageExecutionContext : IDisposable
{
    public StencilMaskImageExecutionContext(SKSizeI imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache, bool invertMask, bool interpolate)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
        InvertMask = invertMask;
        Interpolate = interpolate;
    }

    public SKSizeI ImageSize { get;}

    public ImageDecodingContext DecodingContext { get;}

    public PdfImageTileCacheEntry TileCache { get; }

    public PdfTileInfo TileInfo => TileCache.TileInfo;

    /// <summary>
    /// Whether the stencil mask should be inverted when compositing.
    /// False when the image Decode array is [1 0], true otherwise (default [0 1] behavior).
    /// </summary>
    public bool InvertMask { get; }

    public bool Interpolate { get; }

    public static StencilMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        SKSizeI imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        float[]? decode = pdfImage.DecodeArray;
        bool invertMask = decode == null || decode.Length < 2 || decode[0] < decode[1];

        PdfTileInfo tileInfo = new(imageSize, new SKSizeI(context.DefaultTileSize, context.DefaultTileSize));
        return new StencilMaskImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, context, tileInfo), invertMask, pdfImage.Interpolate);
    }

    public void Dispose() => TileCache.Dispose();
}
