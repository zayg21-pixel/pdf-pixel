using Microsoft.Extensions.Logging;
using PdfPixel.Ccitt;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Streams;
using System;

namespace PdfPixel.Imaging.Decoding;

internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private ReadOnlyMemory<byte>? _data;
    private PdfImageSampleInfo? _sampleInfo;

    public CcittImageDecoder(PdfImage image)
        : base(image)
    {
        _colorSpaceConverter = image.ColorSpace
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
    }

    protected override PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer)
    {
        PdfImageSampleInfo sampleInfo = EnsureSampleInfo();

        _data = Cache.Find(Image, WholeImageKey);

        if (_data == null)
        {
            int rowStride = PdfImageTileSelection.GetRowStride(sampleInfo, sampleInfo.Grid.ImageSize.Width);
            byte[] data = DecodeBitmap(sampleInfo.Grid.ImageSize, rowStride, observer);

            _data = data;
            Cache.Put(Image, WholeImageKey, data);
        }

        return new PdfImageDecodeInfo(sampleInfo, null);
    }

    public override PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer)
    {
        if (_sampleInfo == null || _data == null)
        {
            throw new InvalidOperationException("No tiles are selected.");
        }

        int rowStride = PdfImageTileSelection.GetRowStride(_sampleInfo, _sampleInfo.Grid.ImageSize.Width);

        return CreateBitmapTile(_sampleInfo, _data.Value, rowStride, tileIndex);
    }

    protected override void DropTiles() => _data = null;

    private PdfImageSampleInfo EnsureSampleInfo()
    {
        if (_sampleInfo == null)
        {
            var parameters = Image.DecodeParameters as PdfCcittFaxDecodeParameters;
            int columns = parameters?.Columns ?? Image.Width;
            int rows = parameters?.Rows ?? Image.Height;

            _sampleInfo = new PdfImageSampleInfo(CreateGrid(new PdfIntegerSize(columns, rows)), descaleFactor: 1, Image.BitsPerComponent, 1, _colorSpaceConverter);
        }

        return _sampleInfo;
    }

    private byte[] DecodeBitmap(in PdfIntegerSize size, int rowStride, IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        if (encodedData.IsEmpty)
        {
            throw new InvalidOperationException($"CCITT image data is empty (Reference={Image.Reference}).");
        }

        var parameters = Image.DecodeParameters as PdfCcittFaxDecodeParameters;
        int k = parameters?.CompressionScheme ?? 0;
        bool endOfLine = parameters?.EndOfLine ?? false;
        bool byteAlign = parameters?.EncodedByteAlign ?? false;
        bool blackIs1 = parameters?.BlackIs1 ?? false;
        bool endOfBlock = parameters?.EndOfBlock ?? true;
        int rows = size.Height;

        CcittRowDecoder rowDecoder = new(encodedData, size.Width, rows, blackIs1, k, endOfLine, byteAlign, endOfBlock);
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

        return data;
    }
}
