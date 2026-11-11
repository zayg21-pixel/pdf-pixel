using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.Png;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Work item that adds the requested forms a page's thumbnail is missing to its cache entry: a missing form is made
/// from the form the thumbnail has; a thumbnail with none is produced from the page's thumbnail image when given,
/// otherwise its content without annotations, rendered on white.
/// </summary>
internal sealed class PdfPageThumbnailWorkItem : IWorkItem
{
    private const int ThumbnailBitsPerComponent = 8;
    private const int RgbChannelCount = 3;

    private static readonly PdfRenderingParameters RenderingParameters = new();
    private static readonly PdfImageChannel[] RgbChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2)];

    private readonly PdfPanelPage _page;
    private readonly PdfPanelThumbnailFormats _formats;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfPageThumbnailRasterizer _rasterizer;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<BackgroundWorkItemComplete> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given page and the <paramref name="formats"/> requested of its thumbnail. The
    /// work item takes ownership of <paramref name="observer"/> and invokes <paramref name="onCompleted"/> with its
    /// completion once it finishes.
    /// </summary>
    public PdfPageThumbnailWorkItem(
        PdfPanelPage page,
        PdfPanelThumbnailFormats formats,
        PdfImage? pageThumbnail,
        PdfPageThumbnailRasterizer rasterizer,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<BackgroundWorkItemComplete> onCompleted)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _formats = formats;
        _pageThumbnail = pageThumbnail;
        _rasterizer = rasterizer ?? throw new ArgumentNullException(nameof(rasterizer));
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

            byte[]? png = null;
            PdfImageSamples? samples = null;

            using (LockedContent<PdfPageThumbnail> lockedThumbnail = _page.CacheEntry.Thumbnail.GetContent())
            {
                if (lockedThumbnail.Content != null)
                {
                    png = lockedThumbnail.Content.Png;
                    samples = lockedThumbnail.Content.Samples;
                }
            }

            PdfPanelThumbnailFormats missingFormats = _formats;

            if (png != null)
            {
                missingFormats &= ~PdfPanelThumbnailFormats.Png;
            }

            if (samples != null)
            {
                missingFormats &= ~PdfPanelThumbnailFormats.Samples;
            }

            if (missingFormats != PdfPanelThumbnailFormats.None)
            {
                await AddFormatsAsync(png, samples, missingFormats).ConfigureAwait(false);
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

    /// <summary>
    /// Adds <paramref name="missingFormats"/> to the page's thumbnail, which holds <paramref name="png"/> and
    /// <paramref name="samples"/>, making them from the form it has or producing the thumbnail when it has none.
    /// </summary>
    private async ValueTask AddFormatsAsync(byte[]? png, PdfImageSamples? samples, PdfPanelThumbnailFormats missingFormats)
    {
        if (png == null && samples == null)
        {
            png = DecodePageThumbnail();
        }

        if (samples == null && png != null && (missingFormats & PdfPanelThumbnailFormats.Samples) != 0)
        {
            samples = DecodePng(png);
        }

        if (samples == null && (png == null || (missingFormats & PdfPanelThumbnailFormats.Samples) != 0))
        {
            samples = await RenderPageAsync().ConfigureAwait(false);
        }

        if (png == null && samples != null && (missingFormats & PdfPanelThumbnailFormats.Png) != 0)
        {
            png = PdfImageSamplesPngEncoder.Encode(samples);
        }

        using WriteLockedContent<PdfPageThumbnail> lockedThumbnail = _page.CacheEntry.Thumbnail.GetContentForWriting();
        PdfPageThumbnail? thumbnail = lockedThumbnail.Content;

        if (thumbnail == null)
        {
            return;
        }

        if (thumbnail.Png == null && (missingFormats & PdfPanelThumbnailFormats.Png) != 0)
        {
            thumbnail.Png = png;
        }

        if (thumbnail.Samples == null && (missingFormats & PdfPanelThumbnailFormats.Samples) != 0)
        {
            thumbnail.Samples = samples;
        }
    }

    /// <summary>
    /// Returns the page's thumbnail image as a PNG file, or <see langword="null"/> when no thumbnail image is used or
    /// it cannot be decoded.
    /// </summary>
    private byte[]? DecodePageThumbnail()
    {
        if (_pageThumbnail == null)
        {
            return null;
        }

        PdfImageDecodingParameters decodingParameters = new() { MaximumSize = _rasterizer.MaximumSize };

        lock (_page.PanelDocument.DocumentLocker)
        {
            return _pageThumbnail.DecodePng(decodingParameters, _observer);
        }
    }

    /// <summary>
    /// Decodes an 8-bit RGB thumbnail PNG into samples, or returns <see langword="null"/> for any other kind, which is
    /// rendered instead.
    /// </summary>
    private static PdfImageSamples? DecodePng(byte[] png)
    {
        var info = PngImageInfo.Parse(png);
        PngRowDecoder rows = new(info, png);

        if (rows.BitsPerComponent != ThumbnailBitsPerComponent || rows.ComponentCount != RgbChannelCount)
        {
            return null;
        }

        PdfImageSamples samples = new(rows.Width, rows.Height, PdfImageSampleFormat.Integer, ThumbnailBitsPerComponent, RgbChannels, isPremultiplied: false);

        for (int row = 0; row < rows.Height; row++)
        {
            rows.TryReadRow(samples.GetRow(row));
        }

        return samples;
    }

    private ValueTask<PdfImageSamples> RenderPageAsync()
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

        return _rasterizer.RenderAsync(_page, recording, _optionalContentStates, _observer);
    }
}
