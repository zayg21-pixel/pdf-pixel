using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.Skia;
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
    private readonly ISkSurfaceFactory _surfaceFactory;
    private readonly int _tileSize;
    private readonly Dictionary<int, PageTileCache> _pageCache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageContentTiler"/> class.
    /// </summary>
    /// <param name="surfaceFactory">Factory used to create surfaces for tile rasterization.</param>
    /// <param name="tileSize">Edge length of a single tile in device pixels.</param>
    public PdfPageContentTiler(ISkSurfaceFactory surfaceFactory, int tileSize)
    {
        _surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
        _tileSize = tileSize;
    }

    /// <summary>
    /// Ensures tiles are rasterized for the visible region of the given page.
    /// </summary>
    /// <param name="contentLocker">Locked content picture to rasterize.</param>
    /// <param name="pageInfo">Visible page layout snapshot.</param>
    /// <param name="request">Current drawing request.</param>
    /// <param name="forceClearVisible">When true, re-rasterizes tiles in the visible region.</param>
    public void UpdateTiles(
        ContentLocker<SKPicture>? contentLocker,
        in VisiblePageInfo pageInfo,
        PagesDrawingRequest request,
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

        if (!_pageCache.TryGetValue(pageInfo.PageNumber, out PageTileCache? pageCache))
        {
            pageCache = new PageTileCache();
            _pageCache[pageInfo.PageNumber] = pageCache;
        }

        if (pageCache.Scale != request.Scale)
        {
            pageCache.Clear();
            pageCache.Scale = request.Scale;
        }

        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, pageInfo.Info.Width, pageInfo.Info.Height);

        PdfIntegerRectangle visiblePixels = ToPixels(pageInfo.RegionOfInterest, request.Scale);
        PdfIntegerRectangle pagePixels = ToPixels(pageBounds, request.Scale);

        RasterizeTiles(pageCache, contentLocker, request.Scale, visiblePixels, pagePixels, forceClearVisible);
    }

    /// <summary>
    /// Draws cached tiles for the given page onto the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="pageInfo">Visible page layout snapshot.</param>
    /// <param name="currentScale">Current rendering scale.</param>
    public void DrawTiles(SKCanvas canvas, in VisiblePageInfo pageInfo, float currentScale)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        if (!_pageCache.TryGetValue(pageInfo.PageNumber, out PageTileCache? pageCache))
        {
            return;
        }

        SKSamplingOptions sampling = (pageCache.Scale != currentScale)
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : SKSamplingOptions.Default;

        SKMatrix contentTransform = pageInfo.ContentTransform.ToSkMatrix();
        int savedCount = canvas.Save();
        canvas.Concat(in contentTransform);

        foreach (CachedTile tile in pageCache.Tiles.Values)
        {
            canvas.DrawImage(tile.Image, tile.Destination.ToSkRect(), sampling);
        }

        canvas.RestoreToCount(savedCount);
    }

    /// <summary>
    /// Returns true if there are any cached tiles for the given page.
    /// </summary>
    /// <param name="pageNumber">The page number to check.</param>
    public bool HasTiles(int pageNumber) => _pageCache.TryGetValue(pageNumber, out PageTileCache? pageCache) && pageCache.Tiles.Count > 0;

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

    /// <inheritdoc />
    public void Dispose() => Clear();

    private void RasterizeTiles(
        PageTileCache pageCache,
        ContentLocker<SKPicture> contentLocker,
        float scale,
        in PdfIntegerRectangle visiblePixels,
        in PdfIntegerRectangle pagePixels,
        bool forceClearVisible)
    {
        int endColumn = (visiblePixels.Right + _tileSize - 1) / _tileSize;
        int endRow = (visiblePixels.Bottom + _tileSize - 1) / _tileSize;

        for (int row = visiblePixels.Top / _tileSize; row < endRow; row++)
        {
            for (int column = visiblePixels.Left / _tileSize; column < endColumn; column++)
            {
                PdfIntegerPoint index = new(column, row);

                if (pageCache.Tiles.TryGetValue(index, out CachedTile? cached))
                {
                    if (!forceClearVisible)
                    {
                        continue;
                    }

                    cached.Dispose();
                    pageCache.Tiles.Remove(index);
                }

                CachedTile? tile = RasterizeTile(contentLocker, scale, index, pagePixels);
                if (tile != null)
                {
                    pageCache.Tiles.Add(index, tile);
                }
            }
        }
    }

    private CachedTile? RasterizeTile(
        ContentLocker<SKPicture> contentLocker,
        float scale,
        in PdfIntegerPoint index,
        in PdfIntegerRectangle pagePixels)
    {
        PdfIntegerRectangle tilePixels = new(
            index.X * _tileSize,
            index.Y * _tileSize,
            (index.X + 1) * _tileSize,
            (index.Y + 1) * _tileSize);

        PdfIntegerRectangle coveredPixels = PdfIntegerRectangle.Intersect(tilePixels, pagePixels);

        if (coveredPixels.Width <= 0 || coveredPixels.Height <= 0)
        {
            return null;
        }

        using LockedContent<SKPicture> lockedPicture = contentLocker.GetContent();
        if (lockedPicture.Content == null)
        {
            return null;
        }

        SKSurface tileSurface = _surfaceFactory.GetTilingSurface(_tileSize, _tileSize);
        SKCanvas tileCanvas = tileSurface.Canvas;

        int savedCount = tileCanvas.Save();
        tileCanvas.Clear(SKColors.Transparent);

        tileCanvas.Translate(-tilePixels.Left, -tilePixels.Top);
        tileCanvas.Scale(scale, scale);

        tileCanvas.DrawPicture(lockedPicture.Content);
        tileCanvas.RestoreToCount(savedCount);

        PdfIntegerRectangle surfacePixels = new(
            coveredPixels.Left - tilePixels.Left,
            coveredPixels.Top - tilePixels.Top,
            coveredPixels.Right - tilePixels.Left,
            coveredPixels.Bottom - tilePixels.Top);

        PdfRectangle destination = PdfRectangle.FromLocationAndSize(
            coveredPixels.Left / scale,
            coveredPixels.Top / scale,
            coveredPixels.Width / scale,
            coveredPixels.Height / scale);

        return new CachedTile(tileSurface.Snapshot(surfacePixels.ToSkRectI()), destination);
    }

    private static PdfIntegerRectangle ToPixels(in PdfRectangle rectangle, float scale)
    {
        return new(
            (int)Math.Floor(rectangle.Left * scale),
            (int)Math.Floor(rectangle.Top * scale),
            (int)Math.Ceiling(rectangle.Right * scale),
            (int)Math.Ceiling(rectangle.Bottom * scale));
    }

    private sealed class CachedTile(SKImage image, in PdfRectangle destination) : IDisposable
    {
        public SKImage Image { get; } = image;

        public PdfRectangle Destination { get; } = destination;

        public void Dispose() => Image.Dispose();
    }

    private sealed class PageTileCache
    {
        public Dictionary<PdfIntegerPoint, CachedTile> Tiles { get; } = [];

        public float Scale { get; set; }

        public void Clear()
        {
            foreach (CachedTile tile in Tiles.Values)
            {
                tile.Dispose();
            }

            Tiles.Clear();
        }
    }
}
