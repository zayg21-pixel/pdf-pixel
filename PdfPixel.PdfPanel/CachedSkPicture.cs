using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains a cached <see cref="SKPicture"/> and its thumbnail.
/// </summary>
internal sealed class CachedSkPicture : IDisposable
{
    public CachedSkPicture(SKImage thumbnail, int pageNumber)
    {
        Thumbnail = thumbnail;
        PageNumber = pageNumber;
    }

    public SKPicture Picture { get; private set; }

    public SKPicture AnnitationPicture { get; private set; } // TODO: use

    public float Scale { get; private set; }

    public SKImage Thumbnail { get; }

    public int PageNumber { get; }

    public object DisposeLocker { get; } = new object();

    public bool IsDisposed { get; private set; }

    public void UpdatePicture(SKPicture picture, float scale)
    {
        lock (DisposeLocker)
        {
            Picture?.Dispose();
            Picture = picture;
            Scale = scale;
        }
    }

    public void Dispose()
    {
        lock (DisposeLocker)
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Picture?.Dispose();
            Thumbnail?.Dispose();
        }
    }
}
