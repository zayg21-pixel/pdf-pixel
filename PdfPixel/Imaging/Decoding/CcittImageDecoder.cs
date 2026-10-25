using Microsoft.Extensions.Logging;
using PdfPixel.Ccitt;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private CcittBitmap? _cachedBitmap;
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
        CcittBitmap bitmap = EnsureBitmapDecoded(observer);

        PdfImageRowDecodingParameters rowDecodingParameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(bitmap.Width, bitmap.Height),
            Image.BitsPerComponent,
            _colorSpaceConverter);

        _currentImageRow = 0;

        return rowDecodingParameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_cachedBitmap == null || _currentImageRow >= _cachedBitmap.DecodedRowCount)
        {
            return false;
        }

        _cachedBitmap.GetRowReadOnly(_currentImageRow).CopyTo(destination);
        _currentImageRow++;

        return true;
    }

    public override void Cleanup() => _currentImageRow = 0;

    private CcittBitmap EnsureBitmapDecoded(IPdfExecutionObserver? observer)
    {
        if (_cachedBitmap != null)
        {
            return _cachedBitmap;
        }

        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        if (encodedData.IsEmpty)
        {
            throw new InvalidOperationException($"CCITT image data is empty (Reference={Image.Reference}).");
        }

        var parameters = Image.DecodeParameters as PdfCcittFaxDecodeParameters;
        int columns = parameters?.Columns ?? Image.Width;
        int rows = parameters?.Rows ?? Image.Height;
        int k = parameters?.CompressionScheme ?? 0;
        bool endOfLine = parameters?.EndOfLine ?? false;
        bool byteAlign = parameters?.EncodedByteAlign ?? false;
        bool blackIs1 = parameters?.BlackIs1 ?? false;
        bool endOfBlock = parameters?.EndOfBlock ?? true;

        CcittRowDecoder rowDecoder = new(encodedData, columns, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
        int rowStride = rowDecoder.RowStride;
        var data = new byte[rowStride * rows];
        int decodedRowCount = 0;

        while (decodedRowCount < rows
            && rowDecoder.DecodeNextRow(data.AsSpan(decodedRowCount * rowStride, rowStride)))
        {
            decodedRowCount++;
            observer?.Notify();
        }

        if (decodedRowCount < rows)
        {
            Logger.LogWarning("CCITT row decoder ended early at image row {Row} (Reference={Reference}).", decodedRowCount, Image.Reference);
        }

        _cachedBitmap = new CcittBitmap(columns, rows, rowStride, decodedRowCount, data);

        return _cachedBitmap;
    }

    private sealed class CcittBitmap
    {
        private readonly byte[] _data;

        public CcittBitmap(int width, int height, int rowStride, int decodedRowCount, byte[] data)
        {
            Width = width;
            Height = height;
            RowStride = rowStride;
            DecodedRowCount = decodedRowCount;
            _data = data;
        }

        public int Width { get; }

        public int Height { get; }

        public int RowStride { get; }

        public int DecodedRowCount { get; }

        public ReadOnlySpan<byte> GetRowReadOnly(int row) => _data.AsSpan(row * RowStride, RowStride);
    }
}
