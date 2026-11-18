using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// The tiles of a page on the tile grid of one scale and tile size.
/// </summary>
internal sealed class PdfPageTiles
{
    private readonly Dictionary<PdfIntegerPoint, PdfPageTile> _tiles = [];

    /// <summary>
    /// Initializes an empty set on the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/>.
    /// </summary>
    public PdfPageTiles(float scale, int tileSize)
    {
        Scale = scale;
        TileSize = tileSize;
    }

    /// <summary>
    /// Gets the scale the tiles are rasterized at.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Gets the edge length of a tile in device pixels.
    /// </summary>
    public int TileSize { get; }

    /// <summary>
    /// Gets the columns and rows of the tile grid the set holds a tile for.
    /// </summary>
    public PdfIntegerRectangle Grid { get; private set; } = PdfIntegerRectangle.Empty;

    /// <summary>
    /// Gets whether every tile of the set is rasterized.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            foreach (PdfPageTile tile in _tiles.Values)
            {
                if (!tile.IsRasterized)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Returns whether the tiles lie on the tile grid of <paramref name="scale"/> and <paramref name="tileSize"/>.
    /// </summary>
    public bool IsOnGrid(float scale, int tileSize) => Scale == scale && TileSize == tileSize;

    /// <summary>
    /// Makes the set hold a tile for every grid point of <paramref name="grid"/> and no other, the missing ones empty.
    /// </summary>
    /// <returns>The rasterized tiles the set no longer holds.</returns>
    public List<PdfPageTile> Initialize(in PdfIntegerRectangle grid)
    {
        List<PdfIntegerPoint> evictedPoints = [];
        List<PdfPageTile> removedTiles = [];

        foreach (KeyValuePair<PdfIntegerPoint, PdfPageTile> tile in _tiles)
        {
            if (!Contains(grid, tile.Key))
            {
                evictedPoints.Add(tile.Key);
            }
        }

        foreach (PdfIntegerPoint gridPoint in evictedPoints)
        {
            if (_tiles[gridPoint].IsRasterized)
            {
                removedTiles.Add(_tiles[gridPoint]);
            }

            _tiles.Remove(gridPoint);
        }

        for (int row = grid.Top; row < grid.Bottom; row++)
        {
            for (int column = grid.Left; column < grid.Right; column++)
            {
                PdfIntegerPoint gridPoint = new(column, row);

                if (!_tiles.ContainsKey(gridPoint))
                {
                    _tiles.Add(gridPoint, new PdfPageTile(GetDestination(gridPoint)));
                }
            }
        }

        Grid = grid;

        return removedTiles;
    }

    private PdfRectangle GetDestination(in PdfIntegerPoint gridPoint)
    {
        float tileContentSize = TileSize / Scale;

        return PdfRectangle.FromLocationAndSize(gridPoint.X * tileContentSize, gridPoint.Y * tileContentSize, tileContentSize, tileContentSize);
    }

    /// <summary>
    /// Returns the tile at <paramref name="gridPoint"/>, or <see langword="null"/> when it is not in <see cref="Grid"/>.
    /// </summary>
    public PdfPageTile? FindTile(in PdfIntegerPoint gridPoint) => (_tiles.TryGetValue(gridPoint, out PdfPageTile? tile)) ? tile : null;

    /// <summary>
    /// Returns the rasterized tiles of the set.
    /// </summary>
    public List<PdfPageTile> GetRasterizedTiles()
    {
        List<PdfPageTile> tiles = [];

        foreach (PdfPageTile tile in _tiles.Values)
        {
            if (tile.IsRasterized)
            {
                tiles.Add(tile);
            }
        }

        return tiles;
    }

    /// <summary>
    /// Returns the tiles of <see cref="Grid"/> row by row.
    /// </summary>
    public IEnumerable<KeyValuePair<PdfIntegerPoint, PdfPageTile>> GetTiles()
    {
        for (int row = Grid.Top; row < Grid.Bottom; row++)
        {
            for (int column = Grid.Left; column < Grid.Right; column++)
            {
                PdfIntegerPoint gridPoint = new(column, row);

                yield return new KeyValuePair<PdfIntegerPoint, PdfPageTile>(gridPoint, _tiles[gridPoint]);
            }
        }
    }

    private static bool Contains(in PdfIntegerRectangle grid, in PdfIntegerPoint gridPoint)
    {
        return gridPoint.X >= grid.Left
            && gridPoint.X < grid.Right
            && gridPoint.Y >= grid.Top
            && gridPoint.Y < grid.Bottom;
    }
}
