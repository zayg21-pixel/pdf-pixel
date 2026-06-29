using PdfPixel.Commands;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
public sealed class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;
    private bool _wasPartialContent;

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new();

    /// <summary>
    /// Cached full-content picture.
    /// </summary>
    public ContentLocker<SKPicture> ContentPicture { get; } = new();

    /// <summary>
    /// True if content depends on scale and requires generation if scale is updated.
    /// </summary>
    public bool IsScaleDependant { get; private set; }

    /// <summary>
    /// Drawing request that produced the currently cached content picture.
    /// </summary>
    public PagesDrawingRequest? LastRequest { get; private set; }

    /// <summary>
    /// Root of the text block tree extracted during the last content picture generation.
    /// </summary>
    public PdfTextBlock? RootTextBlock { get; private set; }

    /// <summary>
    /// Replace page content with a new command recording. Disposes the previous recording if present.
    /// </summary>
    /// <param name="commandRecording">New command recording. May be null.</param>
    public void UpdateContentCommandRecording(PdfCommandRecorder? commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);

        if (commandRecording != null)
        {
            IsScaleDependant = commandRecording.Commands.Count > 0 && commandRecording.Commands.Any(x => x.IsScaleDependent);
        }
        else
        {
            IsScaleDependant = false;
        }
    }

    /// <summary>
    /// Replace the content picture and remember the request that produced it. Disposes the previous picture if present.
    /// </summary>
    public void UpdateContentPicture(SKPicture? picture, PagesDrawingRequest request, bool isPartialContent, PdfTextBlock? rootTextBlock = null)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ThrowIfDisposed();
        ContentPicture.SetContent(picture);
        LastRequest = request;
        _wasPartialContent = isPartialContent;
        RootTextBlock = rootTextBlock;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the cached picture must be regenerated for <paramref name="request"/>:
    /// no picture is cached yet, no request was recorded, or the content is scale-dependant and the scale changed.
    /// </summary>
    public bool NeedsUpdate(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return !ContentPicture.HasContent
            || LastRequest == null
            || _wasPartialContent
            || (IsScaleDependant && LastRequest.CommandExecutionParameters.ScaleFactor != request.CommandExecutionParameters.ScaleFactor);
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
        _wasPartialContent = false;
        RootTextBlock = null;
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
