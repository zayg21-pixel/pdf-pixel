using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Rasterizes content <see cref="SKPicture"/> recordings into cached tile images
/// for visible page regions.
/// </summary>
public sealed class PdfPageContentTiler : IDisposable
{
    private const int TileSize = 256;

    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly Dictionary<int, PageTileCache> _pageCache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageContentTiler"/> class.
    /// </summary>
    /// <param name="surfaceFactory">Factory used to create surfaces for tile rasterization.</param>
    public PdfPageContentTiler(ISkSurfaceFactory surfaceFactory)
        => _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));

    /// <summary>
    /// Ensures tiles are rasterized for the visible region of the given page.
    /// </summary>
    /// <param name="pageNumber">The page number to update tiles for.</param>
    /// <param name="contentLocker">Locked content picture to rasterize.</param>
    /// <param name="pageInfo">Visible page layout snapshot.</param>
    /// <param name="request">Current drawing request.</param>
    /// <param name="forceClearVisible">When true, re-rasterizes tiles in the visible region.</param>
    public void UpdateTiles(
        int pageNumber,
        ContentLocker<SKPicture>? contentLocker,
        ref readonly VisiblePageInfo pageInfo,
        ref readonly PagesDrawingRequest request,
        bool forceClearVisible)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (contentLocker?.HasContent != true)
        {
            return;
        }

        if (!_pageCache.TryGetValue(pageNumber, out PageTileCache? pageCache))
        {
            pageCache = new PageTileCache();
            _pageCache[pageNumber] = pageCache;
        }

        if (pageCache.Scale != request.Scale)
        {
            pageCache.Clear();
            pageCache.Scale = request.Scale;
        }

        SKRect visibleRegion = ComputeVisibleRegion(in pageInfo, in request);

        if (forceClearVisible)
        {
            ClearTilesInRegion(pageCache, visibleRegion);
        }

        RasterizeVisibleTiles(pageCache, contentLocker, request.Scale, visibleRegion);
    }

    /// <summary>
    /// Draws cached tiles for the given page onto the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="pageNumber">The page number to draw tiles for.</param>
    /// <param name="pageInfo">Visible page layout snapshot.</param>
    /// <param name="currentScale">Current rendering scale.</param>
    public void DrawTiles(SKCanvas canvas, int pageNumber, ref readonly VisiblePageInfo pageInfo, float currentScale)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        if (!_pageCache.TryGetValue(pageNumber, out PageTileCache? pageCache))
        {
            return;
        }

        SKSamplingOptions sampling = (pageCache.Scale != currentScale)
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : SKSamplingOptions.Default;

        SKMatrix contentTransform = pageInfo.ContentTransform;
        int savedCount = canvas.Save();
        canvas.Concat(in contentTransform);

        foreach (CachedTile tile in pageCache.Tiles)
        {
            canvas.DrawImage(tile.Image, tile.Destination, sampling);
        }

        canvas.RestoreToCount(savedCount);
    }

    /// <summary>
    /// Returns true if there are any cached tiles for the given page.
    /// </summary>
    /// <param name="pageNumber">The page number to check.</param>
    public bool HasTiles(int pageNumber) => _pageCache.ContainsKey(pageNumber) && _pageCache[pageNumber].Tiles.Count > 0;

    /// <summary>
    /// Evicts cached tiles for pages not in the given visible set.
    /// </summary>
    /// <param name="visiblePages">Pages to keep tiles for.</param>
    public void EvictExcept(IReadOnlyList<VisiblePageInfo> visiblePages)
    {
        foreach (int pageNumber in _pageCache.Keys.Where(key => !visiblePages.Any(page => page.PageNumber == key)).ToList())
        {
            _pageCache[pageNumber].Clear();
            _pageCache.Remove(pageNumber);
        }
    }

    /// <summary>
    /// Clears all cached tiles for all pages.
    /// </summary>
    public void Clear()
    {
        foreach (PageTileCache cache in _pageCache.Values)
        {
            cache.Clear();
        }

        _pageCache.Clear();
    }

    /// <inheritdoc />
    public void Dispose() => Clear();

    private void RasterizeVisibleTiles(
        PageTileCache pageCache,
        ContentLocker<SKPicture> contentLocker,
        float scale,
        SKRect visibleRegion)
    {
        float scaledTileSize = TileSize / scale;

        int startCol = Math.Max(0, (int)Math.Floor(visibleRegion.Left / scaledTileSize));
        int startRow = Math.Max(0, (int)Math.Floor(visibleRegion.Top / scaledTileSize));
        var endCol = (int)Math.Ceiling(visibleRegion.Right / scaledTileSize);
        var endRow = (int)Math.Ceiling(visibleRegion.Bottom / scaledTileSize);

        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                SKRect destination = SKRect.Create(
                    col * scaledTileSize,
                    row * scaledTileSize,
                    scaledTileSize,
                    scaledTileSize);

                if (HasTileAt(pageCache, destination))
                {
                    continue;
                }

                SKImage? tileImage = RasterizeTile(contentLocker, scale, col, row, scaledTileSize);
                if (tileImage != null)
                {
                    pageCache.Tiles.Add(new CachedTile(tileImage, destination));
                }
            }
        }
    }

    private static void ClearTilesInRegion(PageTileCache pageCache, SKRect region)
    {
        for (int i = pageCache.Tiles.Count - 1; i >= 0; i--)
        {
            if (pageCache.Tiles[i].Destination.IntersectsWith(region))
            {
                pageCache.Tiles[i].Dispose();
                pageCache.Tiles.RemoveAt(i);
            }
        }
    }

    private static bool HasTileAt(PageTileCache pageCache, SKRect destination)
    {
        for (int i = 0; i < pageCache.Tiles.Count; i++)
        {
            if (pageCache.Tiles[i].Destination == destination)
            {
                return true;
            }
        }

        return false;
    }

    private SKImage? RasterizeTile(
        ContentLocker<SKPicture> contentLocker,
        float scale,
        int col,
        int row,
        float scaledTileSize)
    {
        using LockedContent<SKPicture> lockedPicture = contentLocker.GetContent();
        if (lockedPicture.Content == null)
        {
            return null;
        }

        SKSurface tileSurface = _surfaceFactory.GetTilingSurface(TileSize, TileSize);
        SKCanvas tileCanvas = tileSurface.Canvas;

        int savedCount = tileCanvas.Save();
        tileCanvas.Clear(SKColors.Transparent);

        tileCanvas.Scale(scale, scale);
        tileCanvas.Translate(-col * scaledTileSize, -row * scaledTileSize);

        tileCanvas.DrawPicture(lockedPicture.Content);
        tileCanvas.RestoreToCount(savedCount);
        tileCanvas.Flush();

        return tileSurface.Snapshot();
    }

    private static SKRect ComputeVisibleRegion(ref readonly VisiblePageInfo pageInfo, ref readonly PagesDrawingRequest request)
    {
        SKMatrix contentToCanvas = pageInfo.GetContentToCanvasMatrix(request.Scale);
        SKRect canvasRect = SKRect.Create(0, 0, request.CanvasSize.Width, request.CanvasSize.Height);
        SKRect regionOfInterest = contentToCanvas.Invert().MapRect(canvasRect);

        SKRect pageBounds = SKRect.Create(0, 0, pageInfo.Info.Width, pageInfo.Info.Height);
        regionOfInterest.Intersect(pageBounds);

        return regionOfInterest;
    }

    private sealed class CachedTile(SKImage image, SKRect destination) : IDisposable
    {
        public SKImage Image { get; } = image;

        public SKRect Destination { get; } = destination;

        public void Dispose() => Image.Dispose();
    }

    private sealed class PageTileCache
    {
        public List<CachedTile> Tiles { get; } = [];

        public float Scale { get; set; }

        public void Clear()
        {
            foreach (CachedTile tile in Tiles)
            {
                tile.Dispose();
            }

            Tiles.Clear();
        }
    }
}
