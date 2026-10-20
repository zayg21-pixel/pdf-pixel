using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.PdfPanel.Execution;
using PdfPixel.PdfPanel.WorkQueue;
using SkiaSharp;
using System;
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
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfIntegerSize _maximumSize;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<BackgroundWorkItemComplete> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given cache entry and thumbnail request. The work item takes ownership of <paramref name="observer"/>
    /// and invokes <paramref name="onCompleted"/> with its completion once it finishes.
    /// </summary>
    public PdfPageThumbnailWorkItem(
        PdfPageCacheEntry cacheEntry,
        PdfPanelDocument panelDocument,
        PdfImage? pageThumbnail,
        in PdfIntegerSize maximumSize,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        _cacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _pageThumbnail = pageThumbnail;
        _maximumSize = maximumSize;
        _optionalContentStates = optionalContentStates;
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    /// <inheritdoc />
    public bool IsSkippable => _observer.IsCancelled;

    /// <inheritdoc />
    public async ValueTask ProcessAsync()
    {
        var result = BackgroundWorkItemResult.Error;

        try
        {
            _observer.Notify();

            if (!_cacheEntry.Thumbnail.HasContent)
            {
                byte[] png = await ProducePngAsync().ConfigureAwait(false);
                _cacheEntry.Thumbnail.SetContent(png);
            }

            result = BackgroundWorkItemResult.Success;
        }
        catch (OperationCanceledException)
        {
            result = BackgroundWorkItemResult.Cancelled;
            throw;
        }
        finally
        {
            _observer.Dispose();
            _onCompleted(new BackgroundWorkItemComplete(_cacheEntry.PageNumber, result, _observer));
        }
    }

    private async ValueTask<byte[]> ProducePngAsync()
    {
        if (_pageThumbnail == null)
        {
            return await RenderPageAsync().ConfigureAwait(false);
        }

        PdfImageDecodingParameters decodingParameters = new() { MaximumSize = _maximumSize };

        lock (_panelDocument.DocumentLocker)
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
            lock (_panelDocument.DocumentLocker)
            {
                recording = _panelDocument.Document.GeneratePageCommandRecording(_cacheEntry.PageNumber, RenderingParameters, _observer);
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
            _panelDocument.Document,
            executionParameters,
            _panelDocument.DocumentLocker,
            _optionalContentStates,
            _observer);

        await PdfDocumentContentExtensions
            .RecordingToSkPictureAsync(recording, executionContext, canvas, GetRenderScale(cropBox, _maximumSize), _panelDocument.Document.LoggerFactory)
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
