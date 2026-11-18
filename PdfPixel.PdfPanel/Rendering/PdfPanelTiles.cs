using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Scheduling;
using PdfPixel.Snap.Rasterization;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Content tiles of the panel's pages, the only cache of them: which tiles each page needs at the current view, and
/// the host's pixels of every tile received, written into resources of its <see cref="IPdfPanelTileResourceFactory"/>
/// and reused for other tiles. A page keeps the tiles of its previous scale until the tiles of the new one cover what
/// it needs.
/// </summary>
public sealed class PdfPanelTiles : IDisposable
{
    private readonly PdfPanelDocument _panelDocument;
    private readonly IPdfPanelTileResourceFactory _resourceFactory;
    private readonly Dictionary<int, PdfPageTileSets> _pages = [];
    private readonly Stack<IPdfPanelTileResource> _freeResources = [];
    private readonly HashSet<int> _changedPageNumbers = [];
    private int _freeResourceTileSize;
    private int _freeResourceCapacity;

    internal PdfPanelTiles(PdfPanelDocument panelDocument, IPdfPanelTileResourceFactory resourceFactory)
    {
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
        _panelDocument.TilesRasterized += OnTilesRasterized;
    }

    /// <summary>
    /// Raised on the UI thread when the tiles <see cref="GetTiles"/> returns for a page changed.
    /// </summary>
    public event EventHandler<PdfPanelTilesChangedEventArgs>? TilesChanged;

    /// <summary>
    /// Returns the rasterized tiles of <paramref name="page"/> in drawing order: the tiles of the scale shown, then the
    /// tiles of the scale being completed over them, each destination snapped to whole panel pixels by the page's
    /// <see cref="PdfPanelFramePage.ContentToPanel"/>, so neighboring tiles share their edges at any scale.
    /// </summary>
    public IReadOnlyList<PdfPanelTile> GetTiles(in PdfPanelFramePage page)
    {
        List<PdfPanelTile> tiles = [];

        if (_pages.TryGetValue(page.PageNumber, out PdfPageTileSets? sets))
        {
            AddTiles(tiles, sets.Current, page.ContentToPanel);
            AddTiles(tiles, sets.Next, page.ContentToPanel);
        }

        return tiles;
    }

    /// <summary>
    /// Brings the tile sets of <paramref name="pages"/>, the first <paramref name="visiblePageCount"/> of them visible,
    /// to the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/>, releases the tiles of the pages no
    /// longer among them, keeps as many released resources as the tiles a region of <paramref name="panelSize"/>
    /// reaches, one column and one row more than it spans, and returns the request pages with the tiles each page
    /// still needs: the empty ones and the ones rasterized with other settings or annotation states.
    /// </summary>
    internal ContentUpdatePage[] Update(
        RequestPageInfo[] pages,
        int visiblePageCount,
        float scale,
        int tileSize,
        bool antialias,
        PdfOptionalContentStates optionalContentStates,
        PdfAnnotationStates annotationStates,
        in PdfSize panelSize)
    {
        UpdateFreeResources(tileSize, panelSize);

        HashSet<int> pageNumbers = [];
        var requestPages = new ContentUpdatePage[pages.Length];

        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            RequestPageInfo page = pages[pageIndex];
            bool isVisible = pageIndex < visiblePageCount;
            PdfRectangle region;

            if (isVisible)
            {
                region = page.VisibleRegion;
            }
            else
            {
                region = PdfRectangle.FromLocationAndSize(0, 0, page.Page.Size.Width, page.Page.Size.Height);
            }

            pageNumbers.Add(page.PageNumber);

            PdfPageTiles target = GetTargetSet(page.PageNumber, scale, tileSize);
            ReleaseTiles(page.PageNumber, target.Initialize(PdfPageContentTiler.GetTileGrid(page.Page, region, scale, tileSize)));
            PromoteCompleteSet(page.PageNumber);

            List<PdfIntegerPoint> gridPoints = [];

            foreach (KeyValuePair<PdfIntegerPoint, PdfPageTile> tile in target.GetTiles())
            {
                if (tile.Value.IsOutOfDate(antialias, optionalContentStates, annotationStates, page.Page))
                {
                    gridPoints.Add(tile.Key);
                }
            }

            requestPages[pageIndex] = new ContentUpdatePage(page.Page, isVisible, gridPoints);
        }

        List<int> removedPageNumbers = [];

        foreach (int pageNumber in _pages.Keys)
        {
            if (!pageNumbers.Contains(pageNumber))
            {
                removedPageNumbers.Add(pageNumber);
            }
        }

        foreach (int pageNumber in removedPageNumbers)
        {
            RemovePage(pageNumber);
        }

        RaiseTilesChanged();

        return requestPages;
    }

    /// <summary>
    /// Releases the tiles of every page.
    /// </summary>
    internal void Clear()
    {
        List<int> pageNumbers = [.. _pages.Keys];

        foreach (int pageNumber in pageNumbers)
        {
            RemovePage(pageNumber);
        }

        RaiseTilesChanged();
    }

    private static void AddTiles(List<PdfPanelTile> tiles, PdfPageTiles? set, in PdfMatrix contentToPanel)
    {
        if (set == null)
        {
            return;
        }

        foreach (PdfPageTile tile in set.GetRasterizedTiles())
        {
            if (tile.Resource != null)
            {
                tiles.Add(new PdfPanelTile(tile.Resource, PdfSnapGrid.SnapToWholeDevicePixels(tile.Destination, contentToPanel)));
            }
        }
    }

    /// <summary>
    /// Returns the set of the page of <paramref name="pageNumber"/> the tiles of <paramref name="scale"/> and
    /// <paramref name="tileSize"/> go to: the shown set when it lies on that grid, which drops the set being completed;
    /// otherwise the set being completed, started again on that grid when it lies on another, or the shown set itself
    /// when the page has none.
    /// </summary>
    private PdfPageTiles GetTargetSet(int pageNumber, float scale, int tileSize)
    {
        if (!_pages.TryGetValue(pageNumber, out PdfPageTileSets? sets))
        {
            sets = new PdfPageTileSets();
            _pages.Add(pageNumber, sets);
        }

        if (sets.Current == null)
        {
            sets.Current = new PdfPageTiles(scale, tileSize);

            return sets.Current;
        }

        if (sets.Current.IsOnGrid(scale, tileSize))
        {
            DropNextSet(pageNumber, sets);

            return sets.Current;
        }

        if (sets.Next == null || !sets.Next.IsOnGrid(scale, tileSize))
        {
            DropNextSet(pageNumber, sets);
            sets.Next = new PdfPageTiles(scale, tileSize);
        }

        return sets.Next;
    }

    /// <summary>
    /// Makes the set being completed of the page of <paramref name="pageNumber"/> the shown one when it is complete,
    /// releasing the tiles of the set it replaces.
    /// </summary>
    private void PromoteCompleteSet(int pageNumber)
    {
        PdfPageTileSets sets = _pages[pageNumber];

        if (sets.Next == null || !sets.Next.IsComplete)
        {
            return;
        }

        PdfPageTiles? replaced = sets.Current;
        sets.Current = sets.Next;
        sets.Next = null;

        if (replaced != null)
        {
            ReleaseTiles(pageNumber, replaced.GetRasterizedTiles());
        }
    }

    private void DropNextSet(int pageNumber, PdfPageTileSets sets)
    {
        if (sets.Next == null)
        {
            return;
        }

        PdfPageTiles dropped = sets.Next;
        sets.Next = null;
        ReleaseTiles(pageNumber, dropped.GetRasterizedTiles());
    }

    private void RemovePage(int pageNumber)
    {
        PdfPageTileSets sets = _pages[pageNumber];
        _pages.Remove(pageNumber);

        DropNextSet(pageNumber, sets);

        if (sets.Current != null)
        {
            ReleaseTiles(pageNumber, sets.Current.GetRasterizedTiles());
        }
    }

    /// <summary>
    /// Keeps the released resources of <paramref name="tileSize"/>, as many as the tiles a region of
    /// <paramref name="panelSize"/> reaches, one column and one row more than it spans, disposing the others.
    /// </summary>
    private void UpdateFreeResources(int tileSize, in PdfSize panelSize)
    {
        int columns = (int)Math.Ceiling(panelSize.Width / tileSize) + 1;
        int rows = (int)Math.Ceiling(panelSize.Height / tileSize) + 1;
        _freeResourceCapacity = columns * rows;

        if (_freeResourceTileSize != tileSize)
        {
            DisposeFreeResources();
            _freeResourceTileSize = tileSize;
        }

        while (_freeResources.Count > _freeResourceCapacity)
        {
            _freeResources.Pop().Dispose();
        }
    }

    private void DisposeFreeResources()
    {
        while (_freeResources.Count > 0)
        {
            _freeResources.Pop().Dispose();
        }
    }

    private IPdfPanelTileResource RentResource(int tileSize)
    {
        if (tileSize == _freeResourceTileSize && _freeResources.Count > 0)
        {
            return _freeResources.Pop();
        }

        return _resourceFactory.Create(tileSize);
    }

    /// <summary>
    /// Takes back the resources of <paramref name="tiles"/>, which belong to the page of <paramref name="pageNumber"/>
    /// and are no longer held, keeping them for reuse within the capacity and disposing the others.
    /// </summary>
    private void ReleaseTiles(int pageNumber, List<PdfPageTile> tiles)
    {
        if (tiles.Count == 0)
        {
            return;
        }

        foreach (PdfPageTile tile in tiles)
        {
            if (tile.Resource == null)
            {
                continue;
            }

            if (_freeResources.Count < _freeResourceCapacity)
            {
                _freeResources.Push(tile.Resource);
            }
            else
            {
                tile.Resource.Dispose();
            }
        }

        _changedPageNumbers.Add(pageNumber);
    }

    private void RaiseTilesChanged()
    {
        if (_changedPageNumbers.Count == 0)
        {
            return;
        }

        int[] pageNumbers = [.. _changedPageNumbers];
        _changedPageNumbers.Clear();

        foreach (int pageNumber in pageNumbers)
        {
            TilesChanged?.Invoke(this, new PdfPanelTilesChangedEventArgs(pageNumber));
        }
    }

    /// <summary>
    /// Writes <paramref name="tiles"/>, rasterized together for one page at one scale and tile size, into the
    /// resources of their tiles in the set of their page on their grid, when that set still holds their grid point,
    /// raises <see cref="TilesChanged"/> once for the page, then returns their pixels to their pool.
    /// </summary>
    private void OnTilesRasterized(IReadOnlyList<PdfPageRasterizedTile> tiles)
    {
        try
        {
            PdfPageRasterizedTile first = tiles[0];

            if (!_pages.TryGetValue(first.PageNumber, out PdfPageTileSets? sets))
            {
                return;
            }

            PdfPageTiles? target = sets.Find(first.Scale, first.TileSize);

            if (target == null)
            {
                return;
            }

            foreach (PdfPageRasterizedTile tile in tiles)
            {
                PdfPageTile? pageTile = target.FindTile(tile.GridPoint);

                if (pageTile == null)
                {
                    continue;
                }

                IPdfPanelTileResource? resource = pageTile.Resource;

                if (resource == null)
                {
                    resource = RentResource(tile.TileSize);
                }

                resource.Write(tile.Samples);
                pageTile.SetRasterized(resource, tile.Antialias, tile.OptionalContentStates, tile.AnnotationStates);
            }

            _changedPageNumbers.Add(first.PageNumber);
            PromoteCompleteSet(first.PageNumber);
            RaiseTilesChanged();
        }
        finally
        {
            foreach (PdfPageRasterizedTile tile in tiles)
            {
                tile.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _panelDocument.TilesRasterized -= OnTilesRasterized;

        foreach (PdfPageTileSets sets in _pages.Values)
        {
            DisposeResources(sets.Current);
            DisposeResources(sets.Next);
        }

        _pages.Clear();
        DisposeFreeResources();
    }

    private static void DisposeResources(PdfPageTiles? set)
    {
        if (set == null)
        {
            return;
        }

        foreach (PdfPageTile tile in set.GetRasterizedTiles())
        {
            tile.Resource?.Dispose();
        }
    }
}
