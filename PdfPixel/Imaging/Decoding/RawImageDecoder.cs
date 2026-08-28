using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal class RawImageDecoder : PdfImageDecoder
{
    private object? _contentLocker;
    private byte[]? _buffer;
    private Stream? _dataStream;

    public RawImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"Raw image parameters are invalid (SourceReference={Image.SourceReference}).");
        }

        _contentLocker = contentLocker;

        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(Image.Width, Image.Height),
            Image.BitsPerComponent,
            ResolvedColorSpaceConverter);

        _buffer = new byte[parameters.RowBytes];

        lock (contentLocker)
        {
            _dataStream = Image.GetImageDataStream();
        }

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_contentLocker == null || _dataStream == null || _buffer == null)
        {
            return false;
        }

        lock (_contentLocker)
        {
            ReadFull(_dataStream, _buffer);
        }

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
        _contentLocker = null;
    }
}
