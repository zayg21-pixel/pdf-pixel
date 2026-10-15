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
/// Work item that produces the thumbnail of a page as a PNG file: its thumbnail image when given,
/// otherwise its content without annotations, rendered on white.
/// </summary>
internal sealed class PdfPageThumbnailWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new();

    private readonly PdfPageCacheEntry _cacheEntry;
    private readonly IPdfDocument _document;
    private readonly object _documentLocker;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfIntegerSize _maximumSize;
    private readonly PdfIntegerSize _renderSize;
    private readonly float _renderScale;
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
        in PdfIntegerSize renderSize,
        float renderScale,
        IReadOnlyDictionary<PdfReference, bool>? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<PageThumbnailRenderedEventArgs> onRendered)
    {
        _cacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _documentLocker = documentLocker ?? throw new ArgumentNullException(nameof(documentLocker));
        _pageThumbnail = pageThumbnail;
        _maximumSize = maximumSize;
        _renderSize = renderSize;
        _renderScale = renderScale;
        _optionalContentStates = optionalContentStates;
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _onRendered = onRendered ?? throw new ArgumentNullException(nameof(onRendered));
    }

    /// <inheritdoc />
    public bool IsSkippable => false;

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        _observer.Notify();

        byte[] png;

        if (_pageThumbnail != null)
        {
            PdfImageDecodingParameters decodingParameters = new() { MaximumSize = _maximumSize };

            lock (_documentLocker)
            {
                png = _pageThumbnail.DecodePng(decodingParameters, _observer);
            }
        }
        else
        {
            png = await RenderPageAsync().ConfigureAwait(false);
        }

        _onRendered(new PageThumbnailRenderedEventArgs(_cacheEntry.PageNumber, _observer, png));
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

        SKImageInfo imageInfo = new(_renderSize.Width, _renderSize.Height);
        using SKSurface surface = SKSurface.Create(imageInfo);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        PdfCommandExecutionParameters executionParameters = new();
        using PdfCommandExecutionContext executionContext = new(
            _document,
            executionParameters,
            _documentLocker,
            _optionalContentStates,
            _observer);

        await PdfDocumentContentExtensions
            .RecordingToSkPictureAsync(recording, executionContext, canvas, _renderScale, _document.LoggerFactory)
            .ConfigureAwait(false);

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }
}
