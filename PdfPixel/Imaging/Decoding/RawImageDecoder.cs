using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.IO;
using System.Threading;

namespace PdfPixel.Imaging.Decoding;

public class RawImageDecoder : PdfImageDecoder
{
    private Stream _dataStream;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory) { }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, SKMatrix ctm, SKRectI regionOfInterest)
    {
        if (!ValidateImageParameters())
            throw new InvalidOperationException($"Raw image parameters are invalid (Name={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        int rowBytes = checked((Image.Width * Image.ColorSpaceConverter.Components * Image.BitsPerComponent + 7) / 8);
        _fullWidthRowBuffer = new byte[rowBytes];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _dataStream = Image.GetImageDataStream();
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(CancellationToken cancellationToken = default)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!ReadFull(_dataStream, _fullWidthRowBuffer))
            {
                Logger.LogWarning("Premature end of raw stream at image row {Row} (Name={Name}).", _currentImageRow, Image.Name);
                return null;
            }
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, cancellationToken);
            _currentImageRow++;
            cancellationToken.ThrowIfCancellationRequested();
            if (tiles != null) return tiles;
        }
        return null;
    }

    public override void Cleanup()
    {
        _dataStream?.Dispose();
        _dataStream = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _fullWidthRowBuffer = null;
        _currentImageRow = 0;
    }

    private static bool ReadFull(Stream stream, byte[] buffer)
    {
        int bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            if (read == 0) return false;
            bytesRead += read;
        }
        return true;
    }
}
