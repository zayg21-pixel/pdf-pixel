using Microsoft.Extensions.Logging;
using PdfPixel.Ccitt;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private CcittRowDecoder? _rowDecoder;
    private int _currentImageRow;

    public CcittImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        if (encodedData.IsEmpty)
        {
            throw new InvalidOperationException($"CCITT image data is empty (Reference={Image.Reference}).");
        }

        var parameters = Image.DecodeParms as PdfCcittFaxDecodeParameters;
        int columns = parameters?.Columns ?? Image.Width;
        int rows = parameters?.Rows ?? Image.Height;
        int k = parameters?.K ?? 0;
        bool endOfLine = parameters?.EndOfLine ?? false;
        bool byteAlign = parameters?.EncodedByteAlign ?? false;
        bool blackIs1 = parameters?.BlackIs1 ?? false;
        bool endOfBlock = parameters?.EndOfBlock ?? true;

        PdfImageRowDecodingParameters rowDecodingParameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(columns, rows),
            Image.BitsPerComponent,
            _colorSpaceConverter);

        _rowDecoder = new CcittRowDecoder(encodedData, columns, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
        _currentImageRow = 0;

        return rowDecodingParameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_rowDecoder == null)
        {
            return false;
        }

        Span<byte> buffer = destination;
        if (!_rowDecoder.DecodeNextRow(buffer))
        {
            Logger.LogWarning("CCITT row decoder ended early at image row {Row} (Reference={Reference}).", _currentImageRow, Image.Reference);
            return false;
        }

        _currentImageRow++;
        return true;
    }

    public override void Cleanup()
    {
        _rowDecoder = null;
        _currentImageRow = 0;
    }
}
