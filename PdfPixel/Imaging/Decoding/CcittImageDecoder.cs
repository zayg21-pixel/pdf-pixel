using Microsoft.Extensions.Logging;
using PdfPixel.Ccitt;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private CcittRowDecoder? _rowDecoder;
    private byte[]? _fullWidthRowBuffer;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private int _currentImageRow;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    public CcittImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? context.Page.Cache.ColorSpace.ResolveDeviceConverter(1)
            ?? DeviceGrayConverter.Instance;
    }

    protected override PdfColorSpaceConverter ResolvedColorSpaceConverter => _colorSpaceConverter;

    public override void Initialize(PdfTileInfo tileInfo, object contentLocker, in PdfMatrix ctm, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"CCITT image parameters are invalid (SourceReference={Image.SourceReference}).");
        }

        ReadOnlyMemory<byte> encodedData;
        lock (contentLocker)
        {
            encodedData = Image.GetImageData(observer);
        }

        if (encodedData.IsEmpty)
        {
            throw new InvalidOperationException($"CCITT image data is empty (SourceReference={Image.SourceReference}).");
        }

        PdfDecodeParameters? parameters = Image.DecodeParms;
        int columns = parameters?.Columns ?? Image.Width;
        int rows = parameters?.Rows ?? Image.Height;
        int k = parameters?.K ?? 0;
        bool endOfLine = parameters?.EndOfLine ?? false;
        bool byteAlign = parameters?.EncodedByteAlign ?? false;
        bool blackIs1 = parameters?.BlackIs1 ?? false;
        bool endOfBlock = parameters?.EndOfBlock ?? true;

        PdfColorSpaceConverter converter = _colorSpaceConverter;
        PdfIntegerSize? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, new PdfIntegerSize(columns, rows));

        _imageParameters = new PdfImageRowDecodingParameters(
            Context,
            Image.Width,
            Image.Height,
            Image.BitsPerComponent,
            Image.RenderingIntent,
            converter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.Decode,
            downscaledSize);

        _rowDecoder = new CcittRowDecoder(encodedData, columns, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
        _fullWidthRowBuffer = new byte[_rowDecoder.RowStride];
        _tilingContext = new PdfImageTilingContext(tileInfo, _imageParameters, tileIndexesToDecode, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_rowDecoder == null || _imageParameters == null || _fullWidthRowBuffer == null || _tilingContext == null)
        {
            return null;
        }

        Span<byte> buffer = _fullWidthRowBuffer;
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_rowDecoder.DecodeNextRow(ref buffer))
            {
                Logger.LogWarning("CCITT row decoder ended early at image row {Row} (SourceReference={SourceReference}).", _currentImageRow, Image.SourceReference);
                return null;
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

    public override void Cleanup()
    {
        _rowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }
}
