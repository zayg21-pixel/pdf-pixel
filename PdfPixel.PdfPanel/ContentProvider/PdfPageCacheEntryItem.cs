using PdfPixel.Commands;
using PdfPixel.PdfPanel.Extensions;
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
    /// Visible region within the page content coordinate space used when the current picture was decoded.
    /// </summary>
    public SKRect LastRegionOfInterest { get; private set; }

    /// <summary>
    /// True when the cached picture covers only a subset of the full content.
    /// </summary>
    public bool IsPartialContent { get; private set; }

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
    public void UpdateContent(SKPicture? picture, PagesDrawingRequest request, bool isPartialContent, List<PdfCharacter>? characters = null)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ThrowIfDisposed();
        ContentPicture.SetContent(picture);
        LastRequest = request;
        LastRegionOfInterest = request.ComputeRegionOfInterest(PageNumber);
        IsPartialContent = isPartialContent;
        Characters = characters;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the cached picture must be regenerated for <paramref name="request"/>:
    /// no picture is cached yet, no request was recorded, or a feature-specific dependency changed.
    /// </summary>
    public bool NeedsUpdate(PagesDrawingRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return !ContentPicture.HasContent
            || LastRequest == null
            || ((Features & PdfCommandFeatures.Scale) != 0 && LastRequest.CommandExecutionParameters.ScaleFactor != request.CommandExecutionParameters.ScaleFactor)
            || ((Features & PdfCommandFeatures.Region) != 0 && LastRegionOfInterest != request.ComputeRegionOfInterest(PageNumber));
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
        LastRegionOfInterest = default;
        IsPartialContent = false;
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
