using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
public sealed class PdfPageCacheEntry : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a cache entry for the given page number, info, and annotations.
    /// </summary>
    public PdfPageCacheEntry(int pageNumber, in PdfPanelPageInfo pageInfo, PdfAnnotationPopup[]? annotations)
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
    public PdfPageCacheEntryItem Content { get; } = new();

    /// <summary>
    /// Page annotation content.
    /// </summary>
    public PdfPageCacheEntryItem AnnotationContent { get; } = new();

    /// <summary>
    /// Page annotations.
    /// </summary>
    public PdfAnnotationPopup[]? Annotations { get; }

    /// <summary>
    /// Current active annotation for the page.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; private set; }

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
    public CancellationTokenSource? ParseCancellationTokenSource { get; private set; }

    /// <summary>
    /// Request processing cancellation token source.
    /// </summary>
    public CancellationTokenSource? ContentCancellationTokenSource { get; private set; }

    /// <summary>
    /// Rendering parameters used to generate the cached content.
    /// </summary>
    public PdfRenderingParameters? RenderingParameters { get; private set; }

    /// <summary>
    /// Returns <see langword="true"/> when the entry needs to be re-decoded for the given request.
    /// </summary>
    public bool NeedsUpdate(UpdateContentRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool neverDecoded = !PendingRequest;
        bool parametersChanged = !request.RenderingParameters.Equals(RenderingParameters);
        bool annotationStateChanged = Annotations?.Length > 0
            && (ActiveAnnotation != request.ActiveAnnotation
                || CurrentPointerState != request.PointerState);

        return neverDecoded || parametersChanged || annotationStateChanged;
    }

    /// <summary>
    /// Prepares the entry for a new decode pass, cancelling any in-progress content work and storing the request parameters.
    /// </summary>
    public void InitializeForRendering(UpdateContentRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ThrowIfDisposed();

        ParseCancellationTokenSource ??= new CancellationTokenSource();

        CancelPendingRequest(ContentCancellationTokenSource);
        ContentCancellationTokenSource = new CancellationTokenSource();

        RenderingParameters = request.RenderingParameters.Clone();
        PendingRequest = true;
    }

    /// <summary>
    /// Cancels any in-progress work tokens. Safe to call from any thread.
    /// </summary>
    public void Cancel()
    {
        ThrowIfDisposed();

        CancelPendingRequest(ParseCancellationTokenSource);
        CancelPendingRequest(ContentCancellationTokenSource);

        ParseCancellationTokenSource = null;
        ContentCancellationTokenSource = null;

        PendingRequest = false;
    }

    /// <summary>
    /// Clears cached content. Must be called from the worker thread.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        Content.Clear();
        AnnotationContent.Clear();
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
    public void UpdateActiveAnnotationState(PdfAnnotationPopup? activeAnnotation, PdfPanelPointerState pointerState)
    {
        ThrowIfDisposed();
        ActiveAnnotation = activeAnnotation;
        CurrentPointerState = pointerState;
    }

    private void CancelPendingRequest(CancellationTokenSource? cancellationTokenSource)
    {
        try
        {
            if (cancellationTokenSource?.IsCancellationRequested == false)
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

        Cancel();
        Clear();
        _disposed = true;
    }
}
