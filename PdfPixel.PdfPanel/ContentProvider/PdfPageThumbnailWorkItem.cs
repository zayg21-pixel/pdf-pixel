using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Work item that produces the thumbnail of a page as a PNG file and stores it in the page's cache entry:
/// its thumbnail image when given, otherwise its content without annotations, rendered on white.
/// </summary>
internal sealed class PdfPageThumbnailWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new();

    private readonly PdfPageCacheEntry _cacheEntry;
    private readonly IPdfDocument _document;
    private readonly object _documentLocker;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfIntegerSize _maximumSize;
    private readonly IReadOnlyDictionary<PdfReference, bool>? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<PageThumbnailRenderedEventArgs> _onRendered;

    /// <summary>
    /// Initializes the work item for the given cache entry and thumbnail request.
    /// </summary>
    public PdfPageThumbnailWorkItem(
        PdfPageCacheEntry cacheEntry,
        IPdfDocument document,
        object documentLocker,
        PdfImage? pageThumbnail,
        in PdfIntegerSize maximumSize,
        IReadOnlyDictionary<PdfReference, bool>? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<PageThumbnailRenderedEventArgs> onRendered)
    {
        _cacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _documentLocker = documentLocker ?? throw new ArgumentNullException(nameof(documentLocker));
        _pageThumbnail = pageThumbnail;
        _maximumSize = maximumSize;
        _optionalContentStates = optionalContentStates;
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _onRendered = onRendered ?? throw new ArgumentNullException(nameof(onRendered));
    }

    /// <inheritdoc />
    public bool IsSkippable => _observer.IsCancelled;

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        byte[]? png = null;

        try
        {
            _observer.Notify();

            using (LockedContent<byte[]> lockedThumbnail = _cacheEntry.Thumbnail.GetContent())
            {
                png = lockedThumbnail.Content;
            }

            if (png == null)
            {
                png = await ProducePngAsync().ConfigureAwait(false);
                _cacheEntry.Thumbnail.SetContent(png);
            }
        }
        finally
        {
            _onRendered(new PageThumbnailRenderedEventArgs(_cacheEntry.PageNumber, _observer, png));
        }
    }

    private async ValueTask<byte[]> ProducePngAsync()
    {
        if (_pageThumbnail == null)
        {
            return await RenderPageAsync().ConfigureAwait(false);
        }

        PdfImageDecodingParameters decodingParameters = new() { MaximumSize = _maximumSize };

        lock (_documentLocker)
        {
            return _pageThumbnail.DecodePng(decodingParameters, _observer);
        }
    }

    private async ValueTask<byte[]> RenderPageAsync()
    {
        PdfCommandRecorder? recording;

        using (LockedContent<PdfCommandRecorder> lockedRecording = _cacheEntry.Content.ContentCommandRecording.GetContent())
        {
            recording = lockedRecording.Content;
        }

        if (recording == null)
        {
            lock (_documentLocker)
            {
                recording = _document.GeneratePageCommandRecording(_cacheEntry.PageNumber, RenderingParameters, _observer);
            }
        }

        PdfRectangle cropBox = _cacheEntry.PageInfo.CropBox;
        PdfIntegerSize renderSize = GetRenderSize(cropBox, _maximumSize);
        SKImageInfo imageInfo = new(renderSize.Width, renderSize.Height);
        using SKSurface surface = SKSurface.Create(imageInfo);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.SaveLayer();

        PdfCommandExecutionParameters executionParameters = new();
        using PdfCommandExecutionContext executionContext = new(
            _document,
            executionParameters,
            _documentLocker,
            _optionalContentStates,
            _observer);

        await PdfDocumentContentExtensions
            .RecordingToSkPictureAsync(recording, executionContext, canvas, GetRenderScale(cropBox, _maximumSize), _document.LoggerFactory)
            .ConfigureAwait(false);

        canvas.Restore();

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }

    /// <summary>
    /// Returns the size in pixels a page with <paramref name="cropBox"/> is rendered at when fitted within <paramref name="maximumSize"/>.
    /// </summary>
    public static PdfIntegerSize GetRenderSize(in PdfRectangle cropBox, in PdfIntegerSize maximumSize)
    {
        float scale = GetRenderScale(cropBox, maximumSize);

        return new PdfIntegerSize(
            Math.Max(1, (int)Math.Round(cropBox.Width * scale)),
            Math.Max(1, (int)Math.Round(cropBox.Height * scale)));
    }

    private static float GetRenderScale(in PdfRectangle cropBox, in PdfIntegerSize maximumSize)
        => Math.Min(maximumSize.Width / cropBox.Width, maximumSize.Height / cropBox.Height);
}
