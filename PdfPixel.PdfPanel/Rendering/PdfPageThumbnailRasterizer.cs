using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.Snap;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Renders page thumbnails with Snap on one surface the size of the largest thumbnail.
/// </summary>
internal sealed class PdfPageThumbnailRasterizer : IDisposable
{
    private const int ThumbnailBitsPerComponent = 8;

    private static readonly PdfImageChannel[] ThumbnailChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2)];

    private readonly object _locker = new();
    private readonly byte[] _surfaceBuffer;
    private readonly PdfImageSamples _surfaceSamples;
    private readonly PdfSnapSurface _surface;
    private bool _rendering;
    private bool _disposed;

    /// <summary>
    /// Initializes a rasterizer of thumbnails fitted within <paramref name="maximumSize"/>.
    /// </summary>
    /// <param name="maximumSize">Size in pixels a thumbnail is fitted within.</param>
    /// <param name="panelDocument">Document whose pages are rendered.</param>
    /// <param name="surfaceParameters">Parameters of the Snap surface the thumbnails are rendered on.</param>
    public PdfPageThumbnailRasterizer(in PdfIntegerSize maximumSize, PdfPanelDocument panelDocument, PdfSnapSurfaceParameters surfaceParameters)
    {
        if (panelDocument == null)
        {
            throw new ArgumentNullException(nameof(panelDocument));
        }

        MaximumSize = maximumSize;

        PdfCommandExecutionContext initialContext = new(
            panelDocument.CommandCache,
            new PdfCommandExecutionParameters(),
            panelDocument.DocumentLocker,
            panelDocument.Document.OptionalContentProperties.GetDefaultStates(),
            PdfAnnotationStates.Empty,
            new PdfCancellationExecutionObserver(CancellationToken.None));

        int surfaceWidth = Math.Max(1, maximumSize.Width);
        int surfaceHeight = Math.Max(1, maximumSize.Height);

        _surfaceBuffer = new byte[surfaceWidth * surfaceHeight * PdfPageRasterExtensions.RasterBytesPerPixel];
        _surfaceSamples = PdfPageRasterExtensions.CreateRasterSamples(_surfaceBuffer, surfaceWidth, surfaceHeight);
        _surface = new PdfSnapSurface(_surfaceSamples, surfaceParameters, initialContext, panelDocument.Document.LoggerFactory.CreateLogger<PdfSnapSurface>());
    }

    /// <summary>
    /// Gets the size in pixels a thumbnail is fitted within.
    /// </summary>
    public PdfIntegerSize MaximumSize { get; }

    /// <summary>
    /// Renders <paramref name="recording"/>, the content of <paramref name="page"/>, fitted within
    /// <see cref="MaximumSize"/> over white paper and returns it as opaque 8-bit RGB samples; throws
    /// <see cref="OperationCanceledException"/> once the rasterizer is disposed.
    /// </summary>
    /// <param name="page">The page the recording belongs to.</param>
    /// <param name="recording">The content commands of the page.</param>
    /// <param name="optionalContentStates">
    /// The optional content states to render with, or <see langword="null"/> for the document's default configuration.
    /// </param>
    /// <param name="observer">The observer that cancels the rendering.</param>
    public async ValueTask<PdfImageSamples> RenderAsync(
        PdfPanelPage page,
        PdfCommandRecorder recording,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        if (recording == null)
        {
            throw new ArgumentNullException(nameof(recording));
        }

        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        lock (_locker)
        {
            if (_disposed)
            {
                throw new OperationCanceledException();
            }

            _rendering = true;
        }

        try
        {
            return await RenderPageAsync(page, recording, optionalContentStates, observer).ConfigureAwait(false);
        }
        finally
        {
            lock (_locker)
            {
                _rendering = false;

                if (_disposed)
                {
                    _surface.Dispose();
                }
            }
        }
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

    private async ValueTask<PdfImageSamples> RenderPageAsync(
        PdfPanelPage page,
        PdfCommandRecorder recording,
        PdfOptionalContentStates? optionalContentStates,
        IPdfCancellableExecutionObserver observer)
    {
        PdfSize pageSize = page.Size;
        PdfIntegerSize renderSize = GetRenderSize(pageSize, MaximumSize);
        float renderScale = GetRenderScale(pageSize, MaximumSize);
        PdfIntegerRectangle renderPixels = new(0, 0, Math.Min(renderSize.Width, _surface.Width), Math.Min(renderSize.Height, _surface.Height));
        PdfCommandExecutionParameters executionParameters = new() { DeviceProfiles = page.DeviceProfiles };
        PdfOptionalContentStates statesToRender;

        if (optionalContentStates != null)
        {
            statesToRender = optionalContentStates;
        }
        else
        {
            statesToRender = page.PanelDocument.Document.OptionalContentProperties.GetDefaultStates();
        }

        PdfCommandExecutionContext executionContext = new(
            page.PanelDocument.CommandCache,
            executionParameters,
            page.PanelDocument.DocumentLocker,
            statesToRender,
            PdfAnnotationStates.Empty,
            observer);

        PdfPathBuilder renderAreaBuilder = new();
        renderAreaBuilder.AddRect(new PdfRectangle(renderPixels.Left, renderPixels.Top, renderPixels.Right, renderPixels.Bottom));
        PdfPath renderArea = renderAreaBuilder.ToPath();

        _surfaceBuffer.AsSpan().Clear();
        DrawGroupCommand pageGroup;

        lock (page.PanelDocument.DocumentLocker)
        {
            pageGroup = page.PdfPage.CreatePageGroup(new DrawRecordingCommand(recording));
        }

        _surface.Reset();
        _surface.UpdateContext(executionContext);
        await _surface.ProcessAsync(new DrawPathCommand(renderArea, new PdfPaint(PdfPaintStyle.Clip))).ConfigureAwait(false);
        _surface.Concat(PdfMatrix.CreateScale(renderScale, renderScale));
        await _surface.ProcessAsync(pageGroup.GetRootContent()).ConfigureAwait(false);

        PdfImageSamples samples = new(renderPixels.Width, renderPixels.Height, PdfImageSampleFormat.Integer, ThumbnailBitsPerComponent, ThumbnailChannels, isPremultiplied: false);
        _surfaceSamples.CopyOverPaper(renderPixels, samples);

        return samples;
    }

    private static float GetRenderScale(in PdfSize pageSize, in PdfIntegerSize maximumSize)
        => Math.Min(maximumSize.Width / pageSize.Width, maximumSize.Height / pageSize.Height);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_locker)
        {
            _disposed = true;

            if (!_rendering)
            {
                _surface.Dispose();
            }
        }
    }
}
