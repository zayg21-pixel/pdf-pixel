using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class StencilMaskImageExecutionContext : IDisposable
{
    public StencilMaskImageExecutionContext(in PdfIntegerSize imageSize, ImageDecodingContext decodingContext, PdfImageTileCacheEntry tileCache, bool invertMask, bool interpolate)
    {
        ImageSize = imageSize;
        DecodingContext = decodingContext;
        TileCache = tileCache;
        InvertMask = invertMask;
        Interpolate = interpolate;
    }

    public PdfIntegerSize ImageSize { get;}

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
        PdfIntegerSize imageSize = new(pdfImage.Width, pdfImage.Height);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, context, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        PdfRange[]? decode = pdfImage.Decode;
        bool invertMask = decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;

        PdfTileInfo tileInfo = new(imageSize, new PdfIntegerSize(context.DefaultTileSize, context.DefaultTileSize));
        return new StencilMaskImageExecutionContext(imageSize, context, new PdfImageTileCacheEntry(decoder, tileInfo), invertMask, pdfImage.Interpolate);
    }

    public void Dispose() => TileCache.Dispose();
}
