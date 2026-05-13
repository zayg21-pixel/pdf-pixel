using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Ccitt;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private readonly int _k;
    private readonly bool _endOfLine;
    private readonly bool _byteAlign;
    private readonly bool _blackIs1;
    private readonly bool _endOfBlock;
    private readonly int _columns;
    private readonly int _rows;
    private readonly ReadOnlyMemory<byte> _encodedData;

    private CcittRowDecoder _rowDecoder;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public CcittImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
        var parameters = image.DecodeParms;
        _k = parameters?.K ?? 0;
        _endOfLine = parameters?.EndOfLine ?? false;
        _byteAlign = parameters?.EncodedByteAlign ?? false;
        _blackIs1 = parameters?.BlackIs1 ?? false;
        _endOfBlock = parameters?.EndOfBlock ?? false;
        _columns = parameters?.Columns ?? image.Width;
        _rows = parameters?.Rows ?? image.Height;
        _encodedData = image.GetImageData();
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, SKMatrix ctm, SKRectI regionOfInterest)
    {
        int imageWidth = _columns;
        int imageHeight = _rows;
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new InvalidOperationException($"Invalid CCITT dimensions {imageWidth}x{imageHeight} (Name={Image.Name}).");
        if (_encodedData.IsEmpty)
            throw new InvalidOperationException($"CCITT image data is empty (Name={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _rowDecoder = new CcittRowDecoder(_encodedData.Span, imageWidth, imageHeight, _blackIs1, _k, _endOfLine, _byteAlign, _endOfBlock);
        _fullWidthRowBuffer = new byte[_rowDecoder.RowStride];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(CancellationToken cancellationToken = default)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_rowDecoder.DecodeNextRow(_fullWidthRowBuffer))
            {
                Logger.LogWarning("CCITT row decoder ended early at image row {Row} (Name={Name}).", _currentImageRow, Image.Name);
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
        _rowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }
}
