using PdfPixel.Imaging.Decoding;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Caches a single decoded <see cref="SKImage"/> and re-decodes only when the
/// scale (ScaleX / ScaleY of the CTM) changes.
/// The supplied <see cref="ImageDecodingContext"/> is updated with the current CTM
/// before each decode so downstream decoders can compute the correct target size.
/// </summary>
internal class PdfImageCacheEntry : IDisposable
{
    private readonly PdfImageDecoder _decoder;
    private readonly ImageDecodingContext _context;
    private SKMatrix? _cachedMatrix;
    private SKImage _cached;

    public PdfImageCacheEntry(PdfImageDecoder decoder, ImageDecodingContext context)
    {
        _decoder = decoder;
        _context = context;
    }

    public SKImage Getmage(SKMatrix ctm, CancellationToken token)
    {
        if (_decoder == null)
        {
            return null;
        }

        if (_cached != null &&
            _cachedMatrix?.ScaleX == ctm.ScaleX &&
            _cachedMatrix?.ScaleY == ctm.ScaleY)
        {
            return _cached;
        }

        _cached?.Dispose();
        _context.CTM = ctm;
        _cached = _decoder.Decode(_context, token);
        _cachedMatrix = ctm;
        return _cached;
    }

    public void Dispose()
    {
        _cached?.Dispose();
        _cached = null;
        _cachedMatrix = null;
    }
}
