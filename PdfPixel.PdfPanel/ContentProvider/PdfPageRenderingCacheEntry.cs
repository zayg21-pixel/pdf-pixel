using PdfPixel.Commands;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
public sealed class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new ContentLocker<PdfCommandRecorder>();

    /// <summary>
    /// Cached full-content picture.
    /// </summary>
    public ContentLocker<SKPicture> ContentPicture { get; } = new ContentLocker<SKPicture>();

    /// <summary>
    /// True if content depends on scale and requires generation if scale is updated.
    /// </summary>
    public bool IsScaleDependant { get; private set; }

    /// <summary>
    /// Scale used to generate the cached content.
    /// </summary>
    public float Scale { get; private set; } = 1;

    /// <summary>
    /// Replace page content with a new command recording. Disposes the previous recording if present.
    /// </summary>
    /// <param name="commandRecording">New command recording. May be null.</param>
    public void UpdateContentCommandRecording(PdfCommandRecorder commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);

        if (commandRecording != null)
        {
            IsScaleDependant = commandRecording.Commands.Count > 0 && commandRecording.Commands.Any(x => x.IsScaleDependant);
        }
        else
        {
            IsScaleDependant = false;
        }
    }

    /// <summary>
    /// Replace the content picture. Disposes the previous picture if present.
    /// </summary>
    public void UpdateContentPicture(SKPicture picture, float scale)
    {
        ThrowIfDisposed();
        ContentPicture.SetContent(picture);
        Scale = scale;
    }

    /// <summary>
    /// Clears cache entry.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        ContentCommandRecording.SetContent(default);
        ContentPicture.SetContent(default);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntryItem));
        }
    }

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

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
public sealed class PdfPageCacheEntry : IDisposable
{
    private bool _disposed;

    public PdfPageCacheEntry(int pageNumber, PdfPanelPageInfo pageInfo, PdfAnnotationPopup[] annotations)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        PageNumber = pageNumber;
        PageInfo = pageInfo;
        Annotations = annotations;
    }

    /// <summary>
    /// 1-based page number this cache entry represents.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// General information about the page.
    /// </summary>
    public PdfPanelPageInfo PageInfo { get;}

    /// <summary>
    /// Main page content.
    /// </summary>
    public PdfPageCacheEntryItem Content { get; } = new PdfPageCacheEntryItem();

    /// <summary>
    /// Page annotation content.
    /// </summary>
    public PdfPageCacheEntryItem AnnotationContent { get; } = new PdfPageCacheEntryItem();

    /// <summary>
    /// Page annotations.
    /// </summary>
    public PdfAnnotationPopup[] Annotations { get; }

    /// <summary>
    /// Current active annotation for the page.
    /// </summary>
    public PdfAnnotationPopup ActiveAnnotation { get; private set; }

    /// <summary>
    /// Pointer state for page.
    /// </summary>
    public PdfPanelPointerState CurrentPointerState { get; private set; }

    /// <summary>
    /// True if this is pending for update or content is updated.
    /// </summary>
    public bool PendingRequest { get; private set; }

    /// <summary>
    /// Request processing cancellation token source.
    /// </summary>
    public CancellationTokenSource ParseCancellationTokenSource { get; private set; }

    /// <summary>
    /// Request processing cancellation token source.
    /// </summary>
    public CancellationTokenSource ContentCancellationTokenSource { get; private set; }

    /// <summary>
    /// Rendering parameters used to generate the cached content.
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; private set; }

    public void InitializeForRendering(UpdateContentRequest request)
    {
        ThrowIfDisposed();

        ParseCancellationTokenSource ??= new CancellationTokenSource();

        CancelPendingRequest(ContentCancellationTokenSource);
        ContentCancellationTokenSource = new CancellationTokenSource();

        RenderingParameters = request.RenderingParameters.Clone();
        PendingRequest = true;
    }

    /// <summary>
    /// Clears cache entry.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();

        CancelPendingRequest(ParseCancellationTokenSource);
        CancelPendingRequest(ContentCancellationTokenSource);

        ParseCancellationTokenSource = null;
        ContentCancellationTokenSource = null;

        Content.Clear();
        AnnotationContent.Clear();

        PendingRequest = false;
    }

    /// <summary>
    /// Returns collection of content pictures for main content and annotations.
    /// May be used for direct rendering without command recording playback.
    /// </summary>
    /// <returns></returns>
    public PdfContentPictures GetContentPictures()
    {
        ThrowIfDisposed();

        return new PdfContentPictures
        {
            Content = Content.ContentPicture,
            Annotations = AnnotationContent.ContentPicture
        };
    }

    /// <summary>
    /// Updates annotation and pointer state for annotations.
    /// </summary>
    /// <param name="activeAnnotation">Active annotation for the page.</param>
    /// <param name="pointerState">Pointer state for the page.</param>
    public void UpdateActiveAnnotationState(PdfAnnotationPopup activeAnnotation, PdfPanelPointerState pointerState)
    {
        ThrowIfDisposed();
        ActiveAnnotation = activeAnnotation;
        CurrentPointerState = pointerState;
    }

    private void CancelPendingRequest(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }

        cancellationTokenSource?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntry));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Reset();
        _disposed = true;
    }
}
