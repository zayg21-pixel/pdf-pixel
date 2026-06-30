// TODO: [HIGH] cleanup and test the prototype — commented out pending rework of partial content model
/*
using PdfPixel.Commands;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Wraps an <see cref="IPdfCancellableExecutionObserver"/> and performs a partial-picture flush
/// inside <see cref="Notify"/> whenever 200 ms elapse between commands at layer depth 0.
/// Owns the <see cref="SKPictureRecorder"/> and replaces it (and the canvas in
/// <see cref="PdfCommandExecutionContext"/>) on each flush so the next batch of commands
/// records into a fresh surface that already contains the flushed content.
/// </summary>
internal sealed class PartialFlushObserver : IPdfCancellableExecutionObserver
{
    private static readonly TimeSpan FlushThreshold = TimeSpan.FromMilliseconds(500);

    private readonly IPdfCancellableExecutionObserver? _inner;
    private readonly PdfPageCacheEntryItem _cacheContent;
    private readonly PagesDrawingRequest _request;
    private readonly Action? _onPageUpdated;
    private readonly SKRect _pageRect;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private PdfCommandExecutionContext? _executionContext;
    private SKPictureRecorder _recorder = new();
    private IPdfCommand? _lastFlushedCommand;

    /// <summary>
    /// Initializes the observer and starts the first recording session.
    /// Call <see cref="Initialize"/> after constructing the <see cref="PdfCommandExecutionContext"/>
    /// to complete the two-way wiring.
    /// </summary>
    public PartialFlushObserver(
        IPdfCancellableExecutionObserver? inner,
        PdfPageCacheEntryItem cacheContent,
        PagesDrawingRequest request,
        Action? onPageUpdated,
        SKRect pageRect)
    {
        _inner = inner;
        _cacheContent = cacheContent;
        _request = request;
        _onPageUpdated = onPageUpdated;
        _pageRect = pageRect;
    }

    /// <summary>
    /// Starts the initial recording and returns the canvas to pass to <see cref="PdfCommandExecutionContext"/>.
    /// </summary>
    public SKCanvas BeginRecording() => _recorder.BeginRecording(_pageRect);

    /// <summary>
    /// Wires the execution context into this observer after it has been constructed.
    /// Must be called immediately after creating the <see cref="PdfCommandExecutionContext"/>.
    /// </summary>
    public void Initialize(PdfCommandExecutionContext executionContext) => _executionContext = executionContext;

    /// <summary>
    /// Ends the final recording, flushes the canvas, and returns the completed picture.
    /// </summary>
    public SKPicture? EndRecording()
    {
        _executionContext?.Canvas.Flush();
        SKPicture? picture = _recorder.EndRecording();
        _recorder.Dispose();
        return picture;
    }

    /// <inheritdoc />
    public void Notify()
    {
        _inner?.Notify();

        if (_executionContext == null
            || _stopwatch.Elapsed < FlushThreshold
            || _executionContext.Frames.LayerDepth != 0
            || _executionContext.CurrentCommand == _lastFlushedCommand)
        {
            return;
        }

        _executionContext.Canvas.Flush();
        SKPicture partialPicture = _recorder.EndRecording();
        _recorder.Dispose();

        _cacheContent.UpdateContent(partialPicture, _request, regionOfInterest: default, isPartialContent: true);
        _onPageUpdated?.Invoke();

        _recorder = new SKPictureRecorder();
        SKCanvas newCanvas = _recorder.BeginRecording(_pageRect);
        newCanvas.DrawPicture(partialPicture);
        _executionContext.UpdateCanvas(newCanvas);

        _lastFlushedCommand = _executionContext.CurrentCommand;
        _stopwatch.Restart();
    }

    /// <inheritdoc />
    public void Cancel() => _inner?.Cancel();

    /// <inheritdoc />
    public void Dispose() => _recorder.Dispose();
}
*/
