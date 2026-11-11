using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using System;
using System.IO;

namespace PdfPixel.Imaging.Decoding;

internal class RawImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private PdfImageSampleInfo? _sampleInfo;
    private ReadOnlyMemory<byte>? _data;

    public RawImageDecoder(PdfImage image)
        : base(image)
    {
        PdfColorSpaceType defaultColorSpace = (image.BitsPerComponent == 1)
            ? PdfColorSpaceType.DeviceGray
            : PdfColorSpaceType.DeviceRGB;

        _colorSpaceConverter = image.ColorSpace
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(defaultColorSpace);
    }

    protected override PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer)
    {
        PdfImageSampleInfo sampleInfo = EnsureSampleInfo();

        _data = Cache.Find(Image, WholeImageKey);

        if (_data == null)
        {
            int rowStride = PdfImageTileSelection.GetRowStride(sampleInfo, Image.Width);
            ReadOnlyMemory<byte> data = ReadData(rowStride * Image.Height, observer);

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

        int rowStride = PdfImageTileSelection.GetRowStride(_sampleInfo, Image.Width);

        return CreateBitmapTile(_sampleInfo, _data.Value, rowStride, tileIndex);
    }

    protected override void DropTiles() => _data = null;

    private PdfImageSampleInfo EnsureSampleInfo()
    {
        if (_sampleInfo == null)
        {
            int componentCount = (Image.HasImageMask) ? 1 : _colorSpaceConverter.Components;

            _sampleInfo = new PdfImageSampleInfo(CreateGrid(new PdfIntegerSize(Image.Width, Image.Height)), descaleFactor: 1, Image.BitsPerComponent, componentCount, _colorSpaceConverter);
        }

        return _sampleInfo;
    }

    private ReadOnlyMemory<byte> ReadData(int length, IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> data = Image.Stream.DecodeAsMemory(observer);

        if (data.Length < length)
        {
            throw new EndOfStreamException("Premature end of raw stream at image row");
        }

        return data;
    }
}
