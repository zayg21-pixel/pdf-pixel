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
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Rasterizes page content and annotation commands with Snap into the tiles of visible page regions.
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
        _tileSamples = PdfPageRasterExtensions.CreateRasterSamples(_tileBuffer, pool.TileSize, pool.TileSize);
        _surface = new PdfSnapSurface(_tileSamples, surfaceParameters, initialContext, panelDocument.Document.LoggerFactory.CreateLogger<PdfSnapSurface>());
    }

    /// <summary>
    /// Edge length of a single tile in device pixels.
    /// </summary>
    public int TileSize => _pool.TileSize;

    /// <summary>
    /// Rasterizes the tiles of the visible region of <paramref name="page"/> from the content and annotation
    /// recordings of its cache entry into the entry's tiles, the annotations drawn over the content in the page group:
    /// on the grid of the stored set, the set is kept, its tiles outside the region dropped, and the empty ones and
    /// the ones rasterized with other settings or annotation states than <paramref name="request"/> rasterized in
    /// place; on another grid, a new set is rasterized and stored only when it is complete.
    /// </summary>
    /// <param name="page">The page of the request the tiles are rasterized for.</param>
    /// <param name="request">The request the tiles are rasterized for.</param>
    /// <param name="observer">The observer that cancels the rasterization.</param>
    public async ValueTask<TilesUpdateResult> UpdateTilesAsync(ContentUpdatePage page, ContentUpdateRequest request, IPdfCancellableExecutionObserver observer)
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
            return TilesUpdateResult.Unchanged;
        }

        lock (_locker)
        {
            if (_disposed)
            {
                return TilesUpdateResult.Unchanged;
            }

            _rasterizing = true;
        }

        try
        {
            IPdfCommand pageContent = CreatePageGroup(page.Page, content, annotations).GetRootContent();

            return await RasterizeVisibleTilesAsync(cacheEntry.Tiles, pageContent, page, request, observer).ConfigureAwait(false);
        }
        finally
        {
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

    private async ValueTask<TilesUpdateResult> RasterizeVisibleTilesAsync(
        ContentLocker<PdfPageTiles> tiles,
        IPdfCommand pageContent,
        ContentUpdatePage page,
        ContentUpdateRequest request,
        IPdfCancellableExecutionObserver observer)
    {
        PdfIntegerRectangle grid = GetTileGrid(page.Page, page.VisibleRegion, request.Scale, TileSize);
        PdfPageTiles? current;

        using (LockedContent<PdfPageTiles> lockedTiles = tiles.GetContent())
        {
            current = lockedTiles.Content;
        }

        if (current != null && current.IsOnGrid(request.Scale, TileSize))
        {
            using (WriteLockedContent<PdfPageTiles> lockedTiles = tiles.GetContentForWriting())
            {
                current.Initialize(grid);
            }

            return await RasterizeTilesAsync(tiles, current, pageContent, page.Page, request, observer).ConfigureAwait(false);
        }
        else
        {
            PdfPageTiles replacement = new(request.Scale, _pool);
            replacement.Initialize(grid);

            TilesUpdateResult result = await RasterizeTilesAsync(tiles, replacement, pageContent, page.Page, request, observer).ConfigureAwait(false);

            if (result == TilesUpdateResult.Cancelled)
            {
                replacement.Dispose();

                return TilesUpdateResult.Cancelled;
            }
            else
            {
                tiles.SetContent(replacement);

                return TilesUpdateResult.Updated;
            }
        }
    }

    /// <summary>
    /// Rasterizes the empty and out-of-date tiles of <paramref name="target"/>, writing each under the write lock of
    /// <paramref name="tiles"/>.
    /// </summary>
    private async ValueTask<TilesUpdateResult> RasterizeTilesAsync(
        ContentLocker<PdfPageTiles> tiles,
        PdfPageTiles target,
        IPdfCommand pageContent,
        PdfPanelPage page,
        ContentUpdateRequest request,
        IPdfCancellableExecutionObserver observer)
    {
        PdfIntegerRectangle grid = target.Grid;
        PdfIntegerRectangle pagePixels = ToPixels(PdfRectangle.FromLocationAndSize(0, 0, page.Size.Width, page.Size.Height), request.Scale);
        PdfIntegerRectangle gridPixels = new(grid.Left * TileSize, grid.Top * TileSize, grid.Right * TileSize, grid.Bottom * TileSize);
        PdfIntegerRectangle passPixels = PdfIntegerRectangle.Intersect(gridPixels, pagePixels);
        var result = TilesUpdateResult.Unchanged;

        for (int row = grid.Top; row < grid.Bottom && result != TilesUpdateResult.Cancelled; row++)
        {
            for (int column = grid.Left; column < grid.Right && result != TilesUpdateResult.Cancelled; column++)
            {
                PdfIntegerPoint gridPoint = new(column, row);
                PdfPageTile tile = target.GetTile(gridPoint);

                if (!tile.IsOutOfDate(request.Antialias, request.OptionalContentStates, request.AnnotationStates, page))
                {
                    continue;
                }

                PdfIntegerRectangle tilePixels = GetTilePixels(gridPoint);
                PdfRectangle visibleRegion = new(
                    passPixels.Left - tilePixels.Left,
                    passPixels.Top - tilePixels.Top,
                    passPixels.Right - tilePixels.Left,
                    passPixels.Bottom - tilePixels.Top);
                PdfCommandExecutionContext executionContext = CreateExecutionContext(page, request, visibleRegion, observer);

                try
                {
                    RasterizeTile(executionContext, pageContent, request.Scale, tilePixels, pagePixels);

                    if (!observer.IsCancelled)
                    {
                        using (WriteLockedContent<PdfPageTiles> lockedTiles = tiles.GetContentForWriting())
                        {
                            _tileSamples.CopyOverPaper(new PdfIntegerRectangle(0, 0, TileSize, TileSize), tile.Samples);
                            tile.SetRasterized(request.Antialias, request.OptionalContentStates, request.AnnotationStates);
                        }

                        result = TilesUpdateResult.Updated;
                    }

                    await observer.YieldAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    result = TilesUpdateResult.Cancelled;
                }

                if (observer.IsCancelled)
                {
                    result = TilesUpdateResult.Cancelled;
                }
            }
        }

        return result;
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
