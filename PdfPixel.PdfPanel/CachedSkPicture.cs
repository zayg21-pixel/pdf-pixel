using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Contains cached command recordings and a thumbnail for a single PDF page.
/// The page-content recording is produced once and replayed on any canvas with
/// an arbitrary transform. Annotation recordings are regenerated when the active
/// annotation or pointer state changes.
/// </summary>
internal sealed class CachedSkPicture : IDisposable
{
    public CachedSkPicture(PdfCommandRecorder recording, SKImage thumbnail, int pageNumber, bool hasAnnotations)
    {
        Recording = recording;
        Thumbnail = thumbnail;
        PageNumber = pageNumber;
        HasAnnotations = hasAnnotations;
    }

    /// <summary>
    /// Recorded page-content commands. Produced once and replayed onto the final canvas during drawing.
    /// </summary>
    public PdfCommandRecorder Recording { get; }

    /// <summary>
    /// Recorded annotation commands. Replayed on top of page content.
    /// </summary>
    public PdfCommandRecorder AnnotationRecording { get; private set; }

    /// <summary>
    /// Low-resolution thumbnail snapshot of the page content.
    /// </summary>
    public SKImage Thumbnail { get; }

    /// <summary>
    /// 1-based page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Whether the page has any annotation popups.
    /// </summary>
    public bool HasAnnotations { get; }

    /// <summary>
    /// Current pointer state for the active annotation.
    /// </summary>
    public PdfPanelPointerState ActiveAnnotationState { get; set; }

    /// <summary>
    /// The annotation currently under the pointer, if any.
    /// </summary>
    public PdfAnnotationBase ActiveAnnotation { get; set; }

    /// <summary>
    /// Lock object for thread-safe dispose coordination.
    /// </summary>
    public object DisposeLocker { get; } = new object();

    /// <summary>
    /// Whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Replaces the current annotation recording with a new one, disposing the old recording.
    /// </summary>
    /// <param name="recording">The new annotation recording, or null to clear.</param>
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
