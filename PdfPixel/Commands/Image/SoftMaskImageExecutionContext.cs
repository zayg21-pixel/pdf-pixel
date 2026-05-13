using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

internal sealed class SoftMaskImageExecutionContext : IDisposable
{
    public SKSizeI ImageSize { get; private set; }
    public SKSizeI MaskSize { get; private set; }
    public ImageDecodingContext DecodingContext { get; private set; }
    public PdfImageTileCacheEntry ImageCache { get; private set; }
    public PdfImageTileCacheEntry MaskCache { get; private set; }
    public SKColor? Matte { get; private set; }
    public bool Interpolate { get; private set; }
    public PdfTileInfo TileInfo => ImageCache.TileInfo;

    public static SoftMaskImageExecutionContext Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        var maskImage = pdfImage.SoftMask;
        var imageSize = new SKSizeI(pdfImage.Width, pdfImage.Height);
        var maskSize = new SKSizeI(maskImage.Width, maskImage.Height);

        var imageDecoder = PdfImageDecoder.GetDecoder(pdfImage, loggerFactory);
        var maskDecoder = PdfImageDecoder.GetDecoder(maskImage, loggerFactory);

        var (imageTileInfo, maskTileInfo) = PdfImageCommandUtilities.ComputePairedTileSizes(pdfImage, maskImage, context.DefaultTileSize);

        SKColor? matte = null;
        if (maskImage.MatteArray != null)
            matte = maskImage.ColorSpaceConverter.ToSrgb(maskImage.MatteArray, maskImage.RenderingIntent, default);

        return new SoftMaskImageExecutionContext
        {
            ImageSize = imageSize,
            MaskSize = maskSize,
            DecodingContext = context,
            ImageCache = new PdfImageTileCacheEntry(imageDecoder, context, imageTileInfo),
            MaskCache = new PdfImageTileCacheEntry(maskDecoder, context, maskTileInfo),
            Matte = matte,
            Interpolate = pdfImage.Interpolate
        };
    }

    public void Dispose()
    {
        ImageCache.Dispose();
        MaskCache.Dispose();
    }
}
