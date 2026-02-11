using PdfPixel.Annotations.Models;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains a cached <see cref="SKPicture"/> and its thumbnail.
/// </summary>
internal sealed class CachedSkPicture : IDisposable
{
    public CachedSkPicture(SKImage thumbnail, int pageNumber, bool hasAnnotations)
    {
        Thumbnail = thumbnail;
        PageNumber = pageNumber;
        HasAnnotations = hasAnnotations;
    }

    public SKPicture Picture { get; private set; }

    public SKPicture AnnotationPicture { get; private set; }

    public float Scale { get; set; }

    public SKImage Thumbnail { get; }

    public int PageNumber { get; }

    public bool HasAnnotations { get; }

    public PdfPanelPointerState ActiveAnnotationState { get; set; }

    public PdfAnnotationBase ActiveAnnotation { get; set; }

    public object DisposeLocker { get; } = new object();

    public bool IsDisposed { get; private set; }

    public void UpdatePicture(SKPicture picture)
    {
        lock (DisposeLocker)
        {
            Picture?.Dispose();
            Picture = picture;
        }
    }

    public void UpdateAnnotationPicture(SKPicture annotationPicture)
    {
        lock (DisposeLocker)
        {
            AnnotationPicture?.Dispose();
            AnnotationPicture = annotationPicture;
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
            AnnotationPicture?.Dispose();
            Thumbnail?.Dispose();
        }
    }
}
