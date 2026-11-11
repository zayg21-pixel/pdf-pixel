using PdfPixel.Geometry;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// The tiles of a page on the tile grid of one scale and tile size, the annotations drawn over the content in the page
/// group; the tiles' pixels are rented from a <see cref="PdfPageTilePool"/> and returned to it.
/// </summary>
internal sealed class PdfPageTiles : IDisposable
{
    private readonly PdfPageTilePool _pool;
    private readonly Dictionary<PdfIntegerPoint, PdfPageTile> _tiles = [];

    /// <summary>
    /// Initializes an empty set on the tile grid of <paramref name="scale"/> and the tile size of
    /// <paramref name="pool"/>.
    /// </summary>
    public PdfPageTiles(float scale, PdfPageTilePool pool)
    {
        Scale = scale;
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// Gets the scale the tiles are rasterized at.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Gets the edge length of a tile in device pixels.
    /// </summary>
    public int TileSize => _pool.TileSize;

    /// <summary>
    /// Gets the columns and rows of the tile grid the set holds a tile for.
    /// </summary>
    public PdfIntegerRectangle Grid { get; private set; } = PdfIntegerRectangle.Empty;

    /// <summary>
    /// Returns whether the tiles lie on the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/>.
    /// </summary>
    public bool IsOnGrid(float scale, int tileSize) => Scale == scale && TileSize == tileSize;

    /// <summary>
    /// Makes the set hold a tile for every grid point of <paramref name="grid"/> and no other: the tiles outside it
    /// are returned to the pool, the missing ones rented from it, empty.
    /// </summary>
    public void Initialize(in PdfIntegerRectangle grid)
    {
        List<PdfIntegerPoint> evictedPoints = [];

        foreach (PdfIntegerPoint gridPoint in _tiles.Keys)
        {
            if (!Contains(grid, gridPoint))
            {
                evictedPoints.Add(gridPoint);
            }
        }

        foreach (PdfIntegerPoint gridPoint in evictedPoints)
        {
            _pool.Return(_tiles[gridPoint].Samples);
            _tiles.Remove(gridPoint);
        }

        float tileContentSize = TileSize / Scale;

        for (int row = grid.Top; row < grid.Bottom; row++)
        {
            for (int column = grid.Left; column < grid.Right; column++)
            {
                PdfIntegerPoint gridPoint = new(column, row);

                if (!_tiles.ContainsKey(gridPoint))
                {
                    PdfRectangle destination = PdfRectangle.FromLocationAndSize(column * tileContentSize, row * tileContentSize, tileContentSize, tileContentSize);
                    _tiles.Add(gridPoint, new PdfPageTile(_pool.Rent(), destination));
                }
            }
        }

        Grid = grid;
    }

    /// <summary>
    /// Returns the tile at <paramref name="gridPoint"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="gridPoint"/> is not in <see cref="Grid"/>.</exception>
    public PdfPageTile GetTile(in PdfIntegerPoint gridPoint)
    {
        if (!Contains(Grid, gridPoint))
        {
            throw new ArgumentOutOfRangeException(nameof(gridPoint), "The point is not on the grid of the tiles.");
        }

        return _tiles[gridPoint];
    }

    /// <summary>
    /// Returns whether every tile <paramref name="region"/> of the page reaches, in page space within the page
    /// bounds, is rasterized.
    /// </summary>
    public bool Covers(in PdfRectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return true;
        }

        int left = (int)Math.Floor(region.Left * Scale) / TileSize;
        int top = (int)Math.Floor(region.Top * Scale) / TileSize;
        int right = ((int)Math.Ceiling(region.Right * Scale) + TileSize - 1) / TileSize;
        int bottom = ((int)Math.Ceiling(region.Bottom * Scale) + TileSize - 1) / TileSize;

        for (int row = top; row < bottom; row++)
        {
            for (int column = left; column < right; column++)
            {
                PdfIntegerPoint gridPoint = new(column, row);

                if (!Contains(Grid, gridPoint) || !_tiles[gridPoint].IsRasterized)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool Contains(in PdfIntegerRectangle grid, in PdfIntegerPoint gridPoint)
    {
        return gridPoint.X >= grid.Left
            && gridPoint.X < grid.Right
            && gridPoint.Y >= grid.Top
            && gridPoint.Y < grid.Bottom;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (PdfPageTile tile in _tiles.Values)
        {
            _pool.Return(tile.Samples);
        }

        _tiles.Clear();
        Grid = PdfIntegerRectangle.Empty;
    }
}
