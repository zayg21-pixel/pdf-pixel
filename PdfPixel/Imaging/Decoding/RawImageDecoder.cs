using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal class RawImageDecoder : PdfImageDecoder
{
    private object? _contentLocker;
    private Stream? _dataStream;
    private byte[]? _fullWidthRowBuffer;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private int _currentImageRow;

    public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"Raw image parameters are invalid (Name={Image.Name}).");
        }

        _contentLocker = contentLocker;
        int defaultComponents = (Image.BitsPerComponent == 1) ? 1 : 3;
        PdfColorSpaceConverter converter = Image.ColorSpaceConverter ?? Image.Page.Cache.ColorSpace.ResolveDeviceConverter(defaultComponents) ?? DeviceRgbConverter.Instance;
        SKSizeI? downscaledSize = PdfImageRowDecodingParameters.ComputeDownscaledSize(Image.Width, Image.Height, converter, context, ctm);

        _imageParameters = new PdfImageRowDecodingParameters(
            context,
            Image.Width,
            Image.Height,
            Image.BitsPerComponent,
            Image.RenderingIntent,
            converter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.DecodeArray,
            downscaledSize,
            1);

        int rowBytes = checked(((Image.Width * converter.Components * Image.BitsPerComponent) + 7) / 8);
        _fullWidthRowBuffer = new byte[rowBytes];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), tileInfo, _imageParameters, regionOfInterest, LoggerFactory);

        lock (contentLocker)
        {
            _dataStream = Image.GetImageDataStream();
        }

        _currentImageRow = 0;
    }

    public override PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_imageParameters == null || _contentLocker == null || _dataStream == null || _fullWidthRowBuffer == null || _tilingContext == null)
        {
            return null;
        }

        while (_currentImageRow < _imageParameters.Height)
        {
            lock (_contentLocker)
            {
                ReadFull(_dataStream, _fullWidthRowBuffer);
            }

            PdfImageTile[]? tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, observer);
            _currentImageRow++;
            observer?.Notify();

            if (tiles != null)
            {
                return tiles;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadFull(Stream stream, byte[] buffer)
    {
        int bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Premature end of raw stream at image row");
            }

            bytesRead += read;
        }
    }

    public override void Cleanup()
    {
        _dataStream?.Dispose();
        _dataStream = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _fullWidthRowBuffer = null;
        _contentLocker = null;
        _currentImageRow = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dataStream?.Dispose();
            _tilingContext?.Dispose();
        }
    }
}
