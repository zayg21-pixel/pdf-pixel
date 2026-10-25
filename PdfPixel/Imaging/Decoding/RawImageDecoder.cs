using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal class RawImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private byte[]? _buffer;
    private Stream? _dataStream;

    public RawImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        PdfColorSpaceType defaultColorSpace = (image.BitsPerComponent == 1)
            ? PdfColorSpaceType.DeviceGray
            : PdfColorSpaceType.DeviceRGB;

        _colorSpaceConverter = context.ColorSpaceConverter
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(defaultColorSpace);
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(Image.Width, Image.Height),
            Image.BitsPerComponent,
            _colorSpaceConverter);

        _buffer = new byte[parameters.RowBytes];

        _dataStream = Image.Stream.DecodeAsStream();

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_dataStream == null || _buffer == null)
        {
            return false;
        }

        ReadFull(_dataStream, _buffer);

        _buffer.CopyTo(destination);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadFull(Stream stream, in byte[] buffer)
    {
        int bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int read = stream.Read(buffer, bytesRead, buffer.Length - bytesRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Premature end of raw stream at image row");
            }

            bytesRead += read;
        }
    }

    public override void Cleanup()
    {
        _dataStream?.Dispose();
        _dataStream = null;
    }
}
