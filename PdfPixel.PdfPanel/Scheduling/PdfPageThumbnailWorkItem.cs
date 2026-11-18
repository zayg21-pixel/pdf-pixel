using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Png;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Work item that produces a page's thumbnail as opaque 8-bit RGB samples: from the page's thumbnail image when given
/// and decodable to them, otherwise its content without annotations, rendered on white.
/// </summary>
internal sealed class PdfPageThumbnailWorkItem : IWorkItem
{
    private const int ThumbnailBitsPerComponent = 8;
    private const int RgbChannelCount = 3;

    private static readonly PdfRenderingParameters RenderingParameters = new();
    private static readonly PdfImageChannel[] RgbChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2)];

    private readonly PdfPanelPage _page;
    private readonly PdfImage? _pageThumbnail;
    private readonly PdfPageThumbnailRasterizer _rasterizer;
    private readonly PdfOptionalContentStates? _optionalContentStates;
    private readonly IPdfCancellableExecutionObserver _observer;
    private readonly Action<PdfPageRenderedThumbnail> _onCompleted;

    /// <summary>
    /// Initializes the work item for the given page. The work item takes ownership of <paramref name="observer"/> and
    /// hands its thumbnail, or the lack of one when producing it failed, to <paramref name="onCompleted"/> once it
    /// finishes uncancelled.
    /// </summary>
    public PdfPageThumbnailWorkItem(
        PdfPanelPage page,
        PdfImage? pageThumbnail,
        PdfPageThumbnailRasterizer rasterizer,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer,
        Action<PdfPageRenderedThumbnail> onCompleted)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
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
        var isCancelled = false;
        PdfImageSamples? samples = null;

        try
        {
            _observer.Notify();

            byte[]? png = DecodePageThumbnail();

            if (png != null)
            {
                samples = DecodePng(png);
            }

            if (samples == null)
            {
                samples = await RenderPageAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            isCancelled = true;
            throw;
        }
        finally
        {
            _observer.Dispose();

            if (!isCancelled)
            {
                _onCompleted(new PdfPageRenderedThumbnail(_page.PageNumber, _observer, samples));
            }
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
