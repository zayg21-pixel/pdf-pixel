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
    private int _currentImageRow;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    public CcittImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? context.Page.Cache.ColorSpace.ResolveDeviceConverter(1)
            ?? PdfDeviceGrayColorSpaceConverter.Instance;
    }

    protected override PdfColorSpaceConverter ResolvedColorSpaceConverter => _colorSpaceConverter;

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
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

        PdfImageRowDecodingParameters rowDecodingParameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(Image.Width, Image.Height),
            Image.BitsPerComponent,
            _colorSpaceConverter,
            scaledSizeSource: new PdfIntegerSize(columns, rows));

        _rowDecoder = new CcittRowDecoder(encodedData, columns, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
        _fullWidthRowBuffer = new byte[_rowDecoder.RowStride];
        _currentImageRow = 0;

        return rowDecodingParameters;
    }

    public override bool TryReadNextRow(IPdfExecutionObserver? observer, out ReadOnlySpan<byte> row)
    {
        if (_rowDecoder == null || _fullWidthRowBuffer == null)
        {
            row = default;
            return false;
        }

        Span<byte> buffer = _fullWidthRowBuffer;
        if (!_rowDecoder.DecodeNextRow(ref buffer))
        {
            Logger.LogWarning("CCITT row decoder ended early at image row {Row} (SourceReference={SourceReference}).", _currentImageRow, Image.SourceReference);
            row = default;
            return false;
        }

        _currentImageRow++;
        row = _fullWidthRowBuffer;
        return true;
    }

    public override void Cleanup()
    {
        _rowDecoder = null;
        _fullWidthRowBuffer = null;
        _currentImageRow = 0;
    }
}
