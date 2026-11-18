using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.Snap;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Rasterizes page content and annotation commands with Snap into the tiles a content request asks for.
/// </summary>
internal sealed class PdfPageContentTiler : IDisposable
{
    private readonly object _locker = new();
    private readonly PdfPageTilePool _pool;
    private readonly byte[] _tileBuffer;
    private readonly PdfImageSamples _tileSamples;
    private readonly PdfSnapSurface _surface;
    private bool _rasterizing;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageContentTiler"/> class.
    /// </summary>
    /// <param name="panelDocument">Document whose pages are tiled.</param>
    /// <param name="pool">Pool the pixels of the tiles are rented from, its tile size the size of the tiles.</param>
    /// <param name="surfaceParameters">Parameters of the Snap surface the tiles are rasterized on.</param>
    public PdfPageContentTiler(PdfPanelDocument panelDocument, PdfPageTilePool pool, PdfSnapSurfaceParameters surfaceParameters)
    {
        if (panelDocument == null)
        {
            throw new ArgumentNullException(nameof(panelDocument));
        }

        _pool = pool ?? throw new ArgumentNullException(nameof(pool));

        PdfCommandExecutionContext initialContext = new(
            panelDocument.CommandCache,
            new PdfCommandExecutionParameters(),
            panelDocument.DocumentLocker,
            panelDocument.Document.OptionalContentProperties.GetDefaultStates(),
            PdfAnnotationStates.Empty,
            new PdfCancellationExecutionObserver(CancellationToken.None));

        _tileBuffer = new byte[pool.TileSize * pool.TileSize * PdfPageRasterExtensions.RasterBytesPerPixel];
        _tileSamples = PdfPageRasterExtensions.CreateRasterSamples(_tileBuffer, pool.TileSize, pool.TileSize, pool.ChannelOrder);
        _surface = new PdfSnapSurface(_tileSamples, surfaceParameters, initialContext, panelDocument.Document.LoggerFactory.CreateLogger<PdfSnapSurface>());
    }

    /// <summary>
    /// Edge length of a single tile in device pixels.
    /// </summary>
    public int TileSize => _pool.TileSize;

    /// <summary>
    /// Gets the order of the channels of the tiles' pixels.
    /// </summary>
    public PdfPanelTileChannelOrder ChannelOrder => _pool.ChannelOrder;

    /// <summary>
    /// Rasterizes the tiles at the grid points of <paramref name="page"/> from the content and annotation recordings
    /// of its cache entry, the annotations drawn over the content in the page group, until <paramref name="observer"/>
    /// is cancelled, and hands the tiles rasterized to <paramref name="onTilesRasterized"/> at once, which owns them
    /// from then on.
    /// </summary>
    /// <param name="page">The page of the request the tiles are rasterized for.</param>
    /// <param name="request">The request the tiles are rasterized for.</param>
    /// <param name="observer">The observer that cancels the rasterization.</param>
    /// <param name="onTilesRasterized">Receives the tiles rasterized, when there are any.</param>
    public async ValueTask UpdateTilesAsync(
        ContentUpdatePage page,
        ContentUpdateRequest request,
        IPdfCancellableExecutionObserver observer,
        Action<IReadOnlyList<PdfPageRasterizedTile>> onTilesRasterized)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        if (onTilesRasterized == null)
        {
            throw new ArgumentNullException(nameof(onTilesRasterized));
        }

        if (page.GridPoints.Count == 0)
        {
            return;
        }

        PdfPageCacheEntry cacheEntry = page.Page.CacheEntry;
        PdfCommandRecorder? content;
        PdfCommandRecorder? annotations;

        using (LockedContent<PdfCommandRecorder> lockedContent = cacheEntry.Content.ContentCommandRecording.GetContent())
        {
            content = lockedContent.Content;
        }

        using (LockedContent<PdfCommandRecorder> lockedAnnotations = cacheEntry.AnnotationContent.ContentCommandRecording.GetContent())
        {
            annotations = lockedAnnotations.Content;
        }

        if (content == null)
        {
            return;
        }

        lock (_locker)
        {
            if (_disposed)
            {
                return;
            }

            _rasterizing = true;
        }

        List<PdfPageRasterizedTile> rasterizedTiles = [];

        try
        {
            IPdfCommand pageContent = CreatePageGroup(page.Page, content, annotations).GetRootContent();

            await RasterizeTilesAsync(pageContent, page, request, observer, rasterizedTiles).ConfigureAwait(false);
        }
        finally
        {
            if (rasterizedTiles.Count > 0)
            {
                onTilesRasterized(rasterizedTiles);
            }

            lock (_locker)
            {
                _rasterizing = false;

                if (_disposed)
                {
                    _surface.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Expands <paramref name="region"/> outward to the boundaries of the tile grid.
    /// </summary>
    /// <param name="region">Region in content coordinates.</param>
    /// <param name="scale">Rendering scale the tiles are rasterized at.</param>
    /// <param name="tileSize">Edge length of a single tile in device pixels.</param>
    public static PdfRectangle SnapToTileGrid(in PdfRectangle region, float scale, int tileSize)
    {
        float tileContentSize = tileSize / scale;

        return new PdfRectangle(
            MathF.Floor(region.Left / tileContentSize) * tileContentSize,
            MathF.Floor(region.Top / tileContentSize) * tileContentSize,
            MathF.Ceiling(region.Right / tileContentSize) * tileContentSize,
            MathF.Ceiling(region.Bottom / tileContentSize) * tileContentSize);
    }

    /// <summary>
    /// Returns the columns and rows of the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/> whose
    /// tiles <paramref name="region"/> of <paramref name="page"/> reaches within the page bounds.
    /// </summary>
    public static PdfIntegerRectangle GetTileGrid(PdfPanelPage page, in PdfRectangle region, float scale, int tileSize)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        PdfIntegerRectangle pagePixels = ToPixels(PdfRectangle.FromLocationAndSize(0, 0, page.Size.Width, page.Size.Height), scale);
        PdfIntegerRectangle regionPixels = PdfIntegerRectangle.Intersect(ToPixels(region, scale), pagePixels);

        if (regionPixels.Width <= 0 || regionPixels.Height <= 0)
        {
            return PdfIntegerRectangle.Empty;
        }
        else
        {
            return new PdfIntegerRectangle(
                regionPixels.Left / tileSize,
                regionPixels.Top / tileSize,
                (regionPixels.Right + tileSize - 1) / tileSize,
                (regionPixels.Bottom + tileSize - 1) / tileSize);
        }
    }

    /// <summary>
    /// Returns the command that draws <paramref name="content"/> and <paramref name="annotations"/> over it as the page
    /// group of <paramref name="page"/>.
    /// </summary>
    private static DrawGroupCommand CreatePageGroup(PdfPanelPage page, PdfCommandRecorder content, PdfCommandRecorder? annotations)
    {
        PdfCommandRecorder pageGroupContent = new();
        pageGroupContent.Process(new DrawRecordingCommand(content));

        if (annotations != null)
        {
            pageGroupContent.Process(new DrawRecordingCommand(annotations));
        }

        lock (page.PanelDocument.DocumentLocker)
        {
            return page.PdfPage.CreatePageGroup(new DrawRecordingCommand(pageGroupContent));
        }
    }

    /// <summary>
    /// Rasterizes the tiles at the grid points of <paramref name="page"/>, in their order, into
    /// <paramref name="rasterizedTiles"/>, until <paramref name="observer"/> is cancelled.
    /// </summary>
    private async ValueTask RasterizeTilesAsync(
        IPdfCommand pageContent,
        ContentUpdatePage page,
        ContentUpdateRequest request,
        IPdfCancellableExecutionObserver observer,
        List<PdfPageRasterizedTile> rasterizedTiles)
    {
        PdfIntegerRectangle pagePixels = ToPixels(PdfRectangle.FromLocationAndSize(0, 0, page.Page.Size.Width, page.Page.Size.Height), request.Scale);
        PdfIntegerRectangle passPixels = PdfIntegerRectangle.Intersect(GetPassPixels(page.GridPoints), pagePixels);

        foreach (PdfIntegerPoint gridPoint in page.GridPoints)
        {
            if (observer.IsCancelled)
            {
                return;
            }

            PdfIntegerRectangle tilePixels = GetTilePixels(gridPoint);
            PdfRectangle visibleRegion = new(
                passPixels.Left - tilePixels.Left,
                passPixels.Top - tilePixels.Top,
                passPixels.Right - tilePixels.Left,
                passPixels.Bottom - tilePixels.Top);
            PdfCommandExecutionContext executionContext = CreateExecutionContext(page.Page, request, visibleRegion, observer);

            try
            {
                RasterizeTile(executionContext, pageContent, request.Scale, tilePixels, pagePixels);

                if (!observer.IsCancelled)
                {
                    PdfImageSamples samples = _pool.Rent();
                    _tileSamples.CopyOverPaper(new PdfIntegerRectangle(0, 0, TileSize, TileSize), samples);
                    rasterizedTiles.Add(new PdfPageRasterizedTile(
                        page.Page.PageNumber,
                        request.Scale,
                        gridPoint,
                        request.Antialias,
                        request.OptionalContentStates,
                        request.AnnotationStates,
                        samples,
                        _pool));
                }

                await observer.YieldAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Returns the device pixels the tiles at <paramref name="gridPoints"/> span together, from the top-left corner
    /// of the first column and row to the bottom-right corner of the last.
    /// </summary>
    private PdfIntegerRectangle GetPassPixels(IReadOnlyList<PdfIntegerPoint> gridPoints)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        foreach (PdfIntegerPoint gridPoint in gridPoints)
        {
            left = Math.Min(left, gridPoint.X);
            top = Math.Min(top, gridPoint.Y);
            right = Math.Max(right, gridPoint.X + 1);
            bottom = Math.Max(bottom, gridPoint.Y + 1);
        }

        return new PdfIntegerRectangle(left * TileSize, top * TileSize, right * TileSize, bottom * TileSize);
    }

    /// <summary>
    /// Creates the execution context a tile of a pass is replayed with, its visible region the pixels the pass covers
    /// in the device space of the tile.
    /// </summary>
    private static PdfCommandExecutionContext CreateExecutionContext(
        PdfPanelPage page,
        ContentUpdateRequest request,
        in PdfRectangle visibleRegion,
        IPdfCancellableExecutionObserver observer)
    {
        PdfCommandExecutionParameters executionParameters = new() { Antialias = request.Antialias, DeviceProfiles = page.DeviceProfiles };

        return new PdfCommandExecutionContext(
            page.PanelDocument.CommandCache,
            executionParameters,
            page.PanelDocument.DocumentLocker,
            request.OptionalContentStates,
            request.AnnotationStates,
            observer,
            visibleRegion);
    }

    /// <summary>
    /// Rasterizes the tile at <paramref name="tilePixels"/> on the transparent tile surface: the page content clipped to
    /// the part of the page the tile covers.
    /// </summary>
    private void RasterizeTile(
        PdfCommandExecutionContext executionContext,
        IPdfCommand pageContent,
        float scale,
        in PdfIntegerRectangle tilePixels,
        in PdfIntegerRectangle pagePixels)
    {
        PdfIntegerRectangle coveredPixels = PdfIntegerRectangle.Intersect(tilePixels, pagePixels);

        executionContext.ContentScopes.Reset();
        executionContext.ContentTree.Reset();
        _tileBuffer.AsSpan().Clear();
        _surface.Reset();
        _surface.UpdateContext(executionContext);

        PdfPathBuilder coveredArea = new();
        coveredArea.AddRect(new PdfRectangle(coveredPixels.Left, coveredPixels.Top, coveredPixels.Right, coveredPixels.Bottom));
        _surface.Process(new ConcatMatrixCommand(PdfMatrix.CreateTranslation(-tilePixels.Left, -tilePixels.Top)));
        _surface.Process(new DrawPathCommand(coveredArea.ToPath(), new PdfPaint(PdfPaintStyle.Clip)));
        _surface.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(scale, scale)));
        _surface.Process(pageContent);
    }

    private PdfIntegerRectangle GetTilePixels(in PdfIntegerPoint gridPoint)
    {
        return new(
            gridPoint.X * TileSize,
            gridPoint.Y * TileSize,
            (gridPoint.X + 1) * TileSize,
            (gridPoint.Y + 1) * TileSize);
    }

    private static PdfIntegerRectangle ToPixels(in PdfRectangle rectangle, float scale)
    {
        return new(
            (int)Math.Floor(rectangle.Left * scale),
            (int)Math.Floor(rectangle.Top * scale),
            (int)Math.Ceiling(rectangle.Right * scale),
            (int)Math.Ceiling(rectangle.Bottom * scale));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_locker)
        {
            _disposed = true;

            if (!_rasterizing)
            {
                _surface.Dispose();
            }
        }
    }
}
