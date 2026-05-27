using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Decoding;

public class RawImageDecoder : PdfImageDecoder
{
    private object _contentLocker;
    private Stream _dataStream;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"Raw image parameters are invalid (Name={Image.Name}).");
        }

        _contentLocker = contentLocker;
        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        int rowBytes = checked(((Image.Width * Image.ColorSpaceConverter.Components * Image.BitsPerComponent) + 7) / 8);
        _fullWidthRowBuffer = new byte[rowBytes];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);

        lock (contentLocker)
        {
            _dataStream = Image.GetImageDataStream();
        }

        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(IPdfExecutionObserver observer)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            lock (_contentLocker)
            {
                ReadFull(_dataStream, _fullWidthRowBuffer);
            }

            PdfImageTile[] tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, observer);
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
                throw new("Premature end of raw stream at image row");
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
            Cleanup();
        }
    }
}
