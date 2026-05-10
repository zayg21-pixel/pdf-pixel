using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Jbig2.Decoding;
using PdfPixel.Imaging.Jbig2.Model;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// JBIG2 image decoder.
/// Parses JBIG2 segments, decodes generic/text/halftone regions using arithmetic coding,
/// and streams the resulting bi-level bitmap rows through <see cref="PdfImageRowProcessor"/>.
/// </summary>
internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private Jbig2Bitmap _cachedBitmap;
    private ReadOnlyMemory<byte> _imageData;

    /// <summary>
    /// Initializes the JBIG2 image decoder for the given image.
    /// </summary>
    /// <param name="image">Source PDF image.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public Jbig2ImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
        _imageData = image.GetImageData();
        // TODO: [high] pre-heat all data of PdfImage to avoid accessing PDF object.
    }

    /// <inheritdoc/>
    public override SKImage Decode(
        ImageDecodingContext context,
        CancellationToken cancellationToken)
    {
        if (!ValidateImageParameters())
        {
            return null;
        }

        int width = Image.Width;
        int height = Image.Height;

        if (_imageData.IsEmpty)
        {
            Logger.LogError("JBIG2 image data is empty (Name={Name}).", Image.Name);
            return null;
        }

        // Resolve globals cache from the document-level store; decode and cache on first encounter.
        Jbig2SegmentCache globalCache = ResolveGlobalsCache();

        // Decode the JBIG2 page
        if (_cachedBitmap == null)
        {
            var pageDecoder = new Jbig2PageDecoder(Logger);
            _cachedBitmap = pageDecoder.Decode(
                _imageData.Span,
                width,
                height,
                globalCache,
                cancellationToken);

        }

        if (_cachedBitmap == null)
        {
            Logger.LogError("JBIG2 page decoding failed (Name={Name}).", Image.Name);
            return null;
        }

        // Stream the decoded 1-bit rows through PdfImageRowProcessor
        using var rowProcessor = new PdfImageRowProcessor(
            Image,
            LoggerFactory.CreateLogger<PdfImageRowProcessor>(),
            context);

        rowProcessor.InitializeBuffer();

        int stride = _cachedBitmap.Stride;
        byte[] rowBuffer = new byte[stride];

        for (int y = 0; y < height; y++)
        {
            _cachedBitmap.GetRowReadOnly(y).CopyTo(rowBuffer);

            // JBIG2 convention: 1=black, 0=white.
            // PDF DeviceGray convention: 0=black, 1=white.
            // Invert bits so the row processor interprets them correctly.
            for (int b = 0; b < rowBuffer.Length; b++)
            {
                rowBuffer[b] = (byte)~rowBuffer[b];
            }

            rowProcessor.WriteRow(y, rowBuffer);
            cancellationToken.ThrowIfCancellationRequested();

        }

        return rowProcessor.GetDecoded();
    }

    /// <summary>
    /// Returns the <see cref="Jbig2SegmentCache"/> for the /JBIG2Globals stream referenced by this
    /// image's DecodeParms. The cache is built once per globals object reference and stored on the
    /// document's <see cref="PdfDocumentObjectCache"/> for reuse across pages.
    /// Returns <c>null</c> when no /JBIG2Globals entry is present.
    /// </summary>
    private Jbig2SegmentCache ResolveGlobalsCache()
    {
        var globalsObject = Image.DecodeParms?.Jbig2Globals;
        if (globalsObject == null)
        {
            return null;
        }

        var objectCache = Image.SourceObject?.Document?.ObjectCache;
        if (objectCache != null && objectCache.Jbig2GlobalCaches.TryGetValue(globalsObject.Reference, out var existing))
        {
            return existing;
        }

        ReadOnlyMemory<byte> globalsData;
        try
        {
            globalsData = globalsObject.DecodeAsMemory();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to decode JBIG2Globals stream.");
            return null;
        }

        if (globalsData.IsEmpty)
        {
            return null;
        }

        var pageDecoder = new Jbig2PageDecoder(Logger);
        Jbig2SegmentCache globalsCache = pageDecoder.DecodeGlobalCache(globalsData.Span);

        if (objectCache != null)
        {
            objectCache.Jbig2GlobalCaches[globalsObject.Reference] = globalsCache;
        }

        return globalsCache;
    }
}
