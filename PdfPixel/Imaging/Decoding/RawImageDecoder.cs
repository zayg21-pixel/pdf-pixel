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

        lock (contentLocker)
        {
            _dataStream = Image.GetImageDataStream();
        }

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_contentLocker == null || _dataStream == null)
        {
            return false;
        }

        lock (_contentLocker)
        {
            ReadFull(_dataStream, destination);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadFull(Stream stream, in Span<byte> buffer)
    {
        byte[] tempArray = buffer.ToArray(); // TODO: [HIGH] row image decoder is the one who was holding us from using Span, need to use different approach here
        int bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int read = stream.Read(tempArray, bytesRead, buffer.Length - bytesRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Premature end of raw stream at image row");
            }

            bytesRead += read;
        }

        tempArray.CopyTo(buffer);
    }

    public override void Cleanup()
    {
        _dataStream?.Dispose();
        _dataStream = null;
        _contentLocker = null;
    }
}
