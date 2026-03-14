using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains cached command recordings and a thumbnail for a single PDF page.
/// Recordings capture all draw commands at scale 1 (page coordinate space)
/// and can be replayed on any canvas with an arbitrary transform.
/// </summary>
internal sealed class CachedSkPicture : IDisposable
{
    public CachedSkPicture(SKImage thumbnail, int pageNumber, bool hasAnnotations)
    {
        Thumbnail = thumbnail;
        PageNumber = pageNumber;
        HasAnnotations = hasAnnotations;
    }

    /// <summary>
    /// Recorded page-content commands. Replayed onto the final canvas during drawing.
    /// </summary>
    public PdfCommandRecorder Recording { get; private set; }

    /// <summary>
    /// Recorded annotation commands. Replayed on top of page content.
    /// </summary>
    public PdfCommandRecorder AnnotationRecording { get; private set; }

    /// <summary>
    /// The rendering scale at which the recordings were produced.
    /// Recordings are always in page coordinate space (scale 1) but internal
    /// quality decisions (e.g. image down-sampling) depend on this value.
    /// </summary>
    public float Scale { get; set; }

    public SKImage Thumbnail { get; }

    public int PageNumber { get; }

    public bool HasAnnotations { get; }

    public PdfPanelPointerState ActiveAnnotationState { get; set; }

    public PdfAnnotationBase ActiveAnnotation { get; set; }

    public object DisposeLocker { get; } = new object();

    public bool IsDisposed { get; private set; }

    public void UpdateRecording(PdfCommandRecorder recording)
    {
        lock (DisposeLocker)
        {
            Recording?.Dispose();
            Recording = recording;
        }
    }

    public void UpdateAnnotationRecording(PdfCommandRecorder recording)
    {
        lock (DisposeLocker)
        {
            AnnotationRecording?.Dispose();
            AnnotationRecording = recording;
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
            Recording?.Dispose();
            AnnotationRecording?.Dispose();
            Thumbnail?.Dispose();
        }
    }
}
