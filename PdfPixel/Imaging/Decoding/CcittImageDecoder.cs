using Microsoft.Extensions.Logging;
using PdfPixel.Ccitt;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private CcittRowDecoder _rowDecoder;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public CcittImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"CCITT image parameters are invalid (Name={Image.Name}).");
        }

        ReadOnlyMemory<byte> encodedData;
        lock (contentLocker)
        {
            encodedData = Image.GetImageData(observer);
        }

        if (encodedData.IsEmpty)
        {
            throw new InvalidOperationException($"CCITT image data is empty (Name={Image.Name}).");
        }

        PdfDecodeParameters parameters = Image.DecodeParms;
        int columns = parameters?.Columns ?? Image.Width;
        int rows = parameters?.Rows ?? Image.Height;
        int k = parameters?.K ?? 0;
        bool endOfLine = parameters?.EndOfLine ?? false;
        bool byteAlign = parameters?.EncodedByteAlign ?? false;
        bool blackIs1 = parameters?.BlackIs1 ?? false;
        bool endOfBlock = parameters?.EndOfBlock ?? false;

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _rowDecoder = new CcittRowDecoder(encodedData, columns, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
        _fullWidthRowBuffer = new byte[_rowDecoder.RowStride];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(IPdfExecutionObserver observer)
    {
        Span<byte> buffer = _fullWidthRowBuffer;
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_rowDecoder.DecodeNextRow(ref buffer))
            {
                Logger.LogWarning("CCITT row decoder ended early at image row {Row} (Name={Name}).", _currentImageRow, Image.Name);
                return null;
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

    public override void Cleanup()
    {
        _rowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }
}
