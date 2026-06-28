using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Rasterizes content <see cref="SKPicture"/> recordings into fixed-size tile images,
/// caching them per page so that subsequent frames draw a few large images instead
/// of replaying thousands of draw commands.
/// </summary>
public sealed class PdfPageContentTiler : IDisposable
{
    private const int TileSize = 1024;

    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly Dictionary<int, PageTileCache> _pageCache = [];

    /// <inheritdoc cref="PdfPageContentTiler"/>
    public PdfPageContentTiler(ISkSurfaceFactory surfaceFactory)
        => _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));

    /// <summary>
    /// Ensures tiles are rasterized for the visible region of the given page.
    /// </summary>
    public void UpdateTiles(
        int pageNumber,
        ContentLocker<SKPicture> contentLocker,
        ref readonly VisiblePageInfo pageInfo,
        ref readonly PagesDrawingRequest request,
        TileClearMode clearMode)
    {
        if (contentLocker?.HasContent != true)
        {
            return;
        }

        if (!_pageCache.TryGetValue(pageNumber, out PageTileCache? pageCache))
        {
            pageCache = new PageTileCache();
            _pageCache[pageNumber] = pageCache;
        }

        bool shouldClear = clearMode == TileClearMode.ForceClear
            || (clearMode == TileClearMode.ClearOnScaleChange && pageCache.Scale != request.Scale);

        if (shouldClear)
        {
            pageCache.Clear();
            pageCache.Scale = request.Scale;
        }

        SKRect visibleRegion = ComputeVisibleRegion(in pageInfo, in request);
        RasterizeVisibleTiles(pageCache, contentLocker, in pageInfo, request.Scale, visibleRegion);
    }

    /// <summary>
    /// Draws cached tiles for the given page onto the canvas.
    /// </summary>
    public void DrawTiles(SKCanvas canvas, int pageNumber, float currentScale)
    {
        if (!_pageCache.TryGetValue(pageNumber, out PageTileCache? pageCache))
        {
            return;
        }

        float scaledTileSize = TileSize / pageCache.Scale;

        SKSamplingOptions sampling = (pageCache.Scale != currentScale)
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : SKSamplingOptions.Default;

        foreach (KeyValuePair<long, SKImage> entry in pageCache.Tiles)
        {
            var tileX = (int)(entry.Key >> 32);
            var tileY = (int)(entry.Key & 0xFFFFFFFF);

            SKRect dest = new(
                tileX * scaledTileSize,
                tileY * scaledTileSize,
                (tileX + 1) * scaledTileSize,
                (tileY + 1) * scaledTileSize);

            canvas.DrawImage(entry.Value, dest, sampling);
        }
    }

    /// <summary>
    /// Returns true if there are any cached tiles for the given page.
    /// </summary>
    public bool HasTiles(int pageNumber) => _pageCache.ContainsKey(pageNumber) && _pageCache[pageNumber].Tiles.Count > 0;

    /// <summary>
    /// Evicts cached tiles for pages not in the given visible set.
    /// </summary>
    public void EvictExcept(IReadOnlyList<VisiblePageInfo> visiblePages)
    {
        List<int>? toRemove = null;

        foreach (int pageNumber in _pageCache.Keys)
        {
            var found = false;
            for (int i = 0; i < visiblePages.Count; i++)
            {
                if (visiblePages[i].PageNumber == pageNumber)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                toRemove ??= new List<int>();
                toRemove.Add(pageNumber);
            }
        }

        if (toRemove != null)
        {
            foreach (int pageNumber in toRemove)
            {
                _pageCache[pageNumber].Clear();
                _pageCache.Remove(pageNumber);
            }
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
        ref readonly VisiblePageInfo pageInfo,
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
                long tileKey = ((long)col << 32) | (uint)row;

                if (pageCache.Tiles.ContainsKey(tileKey))
                {
                    continue;
                }

                SKImage? tileImage = RasterizeTile(contentLocker, in pageInfo, scale, col, row, scaledTileSize);
                if (tileImage != null)
                {
                    pageCache.Tiles[tileKey] = tileImage;
                }
            }
        }
    }

    private SKImage? RasterizeTile(
        ContentLocker<SKPicture> contentLocker,
        ref readonly VisiblePageInfo pageInfo,
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

        SKMatrix contentTransform = pageInfo.ContentTransform;
        tileCanvas.Concat(in contentTransform);

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

    private sealed class PageTileCache
    {
        public Dictionary<long, SKImage> Tiles { get; } = [];

        public float Scale { get; set; }

        public void Clear()
        {
            foreach (SKImage image in Tiles.Values)
            {
                image.Dispose();
            }

            Tiles.Clear();
        }
    }
}
