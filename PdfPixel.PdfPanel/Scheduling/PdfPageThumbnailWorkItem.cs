using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Caching;
using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Work item that produces the thumbnail of a page as a PNG file and stores it in the page's cache entry:
/// its thumbnail image when given, otherwise its content without annotations, rendered on white.
/// </summary>
internal sealed class PdfPageThumbnailWorkItem : IWorkItem
{
    private static readonly PdfRenderingParameters RenderingParameters = new();

    private readonly PdfPanelPage _page;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfIntegerSize _maximumSize;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<BackgroundWorkItemComplete> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given page and thumbnail request. The work item takes ownership of <paramref name="observer"/>
    /// and invokes <paramref name="onCompleted"/> with its completion once it finishes.
    /// </summary>
    public PdfPageThumbnailWorkItem(
        PdfPanelPage page,
        PdfImage? pageThumbnail,
        in PdfIntegerSize maximumSize,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
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

            if (!_page.CacheEntry.Thumbnail.HasContent)
            {
                byte[] png = await ProducePngAsync().ConfigureAwait(false);
                _page.CacheEntry.Thumbnail.SetContent(png);
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
            _onCompleted(new BackgroundWorkItemComplete(_page.PageNumber, result, _observer));
        }
    }

    private async ValueTask<byte[]> ProducePngAsync()
    {
        if (_pageThumbnail == null)
        {
            return await RenderPageAsync().ConfigureAwait(false);
        }

        PdfImageDecodingParameters decodingParameters = new() { MaximumSize = _maximumSize };

        lock (_page.PanelDocument.DocumentLocker)
        {
            return _pageThumbnail.DecodePng(decodingParameters, _observer);
        }
    }

    private async ValueTask<byte[]> RenderPageAsync()
    {
        PdfCommandRecorder? recording;

        using (LockedContent<PdfCommandRecorder> lockedRecording = _page.CacheEntry.Content.ContentCommandRecording.GetContent())
        {
            recording = lockedRecording.Content;
        }

        if (recording == null)
        {
            lock (_page.PanelDocument.DocumentLocker)
            {
                recording = _page.PanelDocument.Document.GeneratePageCommandRecording(_page.PageNumber, RenderingParameters, _observer);
            }
        }

        PdfSize pageSize = _page.Size;
        PdfIntegerSize renderSize = GetRenderSize(pageSize, _maximumSize);
        SKImageInfo imageInfo = new(renderSize.Width, renderSize.Height);
        using SKSurface surface = SKSurface.Create(imageInfo);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.SaveLayer();

        PdfCommandExecutionParameters executionParameters = new();
        using PdfCommandExecutionContext executionContext = new(
            _page.PanelDocument.Document,
            executionParameters,
            _page.PanelDocument.DocumentLocker,
            _optionalContentStates,
            _observer);

        await PdfDocumentContentExtensions
            .RecordingToSkPictureAsync(recording, executionContext, canvas, GetRenderScale(pageSize, _maximumSize), _page.PanelDocument.Document.LoggerFactory)
            .ConfigureAwait(false);

        canvas.Restore();

        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        return encoded.ToArray();
    }

    /// <summary>
    /// Returns the size in pixels a page of <paramref name="pageSize"/> is rendered at when fitted within <paramref name="maximumSize"/>.
    /// </summary>
    public static PdfIntegerSize GetRenderSize(in PdfSize pageSize, in PdfIntegerSize maximumSize)
    {
        float scale = GetRenderScale(pageSize, maximumSize);

        return new PdfIntegerSize(
            Math.Max(1, (int)Math.Round(pageSize.Width * scale)),
            Math.Max(1, (int)Math.Round(pageSize.Height * scale)));
    }

    private static float GetRenderScale(in PdfSize pageSize, in PdfIntegerSize maximumSize)
        => Math.Min(maximumSize.Width / pageSize.Width, maximumSize.Height / pageSize.Height);
}
