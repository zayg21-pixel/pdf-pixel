using PdfPixel.Commands;
using PdfPixel.Commands.Model;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
public sealed class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes the item for the given page number.
    /// </summary>
    public PdfPageCacheEntryItem(int pageNumber) => PageNumber = pageNumber;

    /// <summary>
    /// 1-based page number this item belongs to.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new();

    /// <summary>
    /// Cached full-content picture.
    /// </summary>
    public ContentLocker<SKPicture> ContentPicture { get; } = new();

    /// <summary>
    /// Combined features of all commands in the current recording.
    /// </summary>
    public PdfCommandFeatures Features { get; private set; }

    /// <summary>
    /// Drawing request that produced the currently cached content picture.
    /// </summary>
    public PagesDrawingRequest? LastRequest { get; private set; }


    /// <summary>
    /// Flattened characters extracted during the last content picture generation, in reading order.
    /// </summary>
    public List<PdfCharacter>? Characters { get; private set; }

    /// <summary>
    /// Replace page content with a new command recording. Disposes the previous recording if present.
    /// </summary>
    /// <param name="commandRecording">New command recording. May be null.</param>
    public void UpdateContentCommandRecording(PdfCommandRecorder? commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);

        Features = (commandRecording != null)
            ? commandRecording.Commands.Aggregate(PdfCommandFeatures.None, (acc, cmd) => acc | cmd.Features)
            : PdfCommandFeatures.None;
    }

    /// <summary>
    /// Replace the content picture and remember the request that produced it. Disposes the previous picture if present.
    /// </summary>
    public void UpdateContent(SKPicture? picture, PagesDrawingRequest request, List<PdfCharacter>? characters = null)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ThrowIfDisposed();
        ContentPicture.SetContent(picture);
        LastRequest = request;
        Characters = characters;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the cached picture must be regenerated for <paramref name="request"/>:
    /// no picture is cached yet, no request was recorded, or a feature-specific dependency changed.
    /// </summary>
    public bool NeedsPictureUpdate(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return !ContentPicture.HasContent
            || LastRequest == null
            || ((Features & PdfCommandFeatures.Scale) != 0 && LastRequest.ScaleFactor != request.ScaleFactor)
            || ((Features & PdfCommandFeatures.Region) != 0 && LastRequest.GetPage(PageNumber).RegionOfInterest != request.GetPage(PageNumber).RegionOfInterest);
    }

    /// <summary>
    /// Scale the content picture is recorded at: the request's scale when a scale-dependent command
    /// was recorded, and 1 otherwise, so that scale-independent content is recorded once and reused
    /// at every scale.
    /// </summary>
    public float GetPictureScale(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return ((Features & PdfCommandFeatures.Scale) != 0) ? request.ScaleFactor : 1f;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the cached annotation recording must be regenerated for <paramref name="request"/>:
    /// no recording is cached yet, no request was recorded, or the active annotation or its state changed.
    /// </summary>
    public bool NeedsAnnotationRecordingUpdate(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return !ContentCommandRecording.HasContent
            || LastRequest == null
            || LastRequest.ActiveAnnotation != request.ActiveAnnotation
            || LastRequest.ActiveAnnotationState != request.ActiveAnnotationState;
    }

    /// <summary>
    /// Clears cache entry.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        ContentCommandRecording.SetContent(default);
        ContentPicture.SetContent(default);
        LastRequest = null;
        Characters = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntryItem));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }
}
