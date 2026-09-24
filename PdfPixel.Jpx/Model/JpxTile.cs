using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Model;

/// <summary>
/// Holds one decoded JPX tile's component samples.
/// </summary>
/// <remarks>
/// A tile is reusable storage: <see cref="Reset"/> re-targets it at another tile of the
/// grid, growing its component buffers only when a larger tile is encountered. Callers
/// that decode a tile grid can therefore keep one instance per tile column alive instead
/// of allocating fresh sample buffers for every tile.
/// </remarks>
public sealed class JpxTile
{
    /// <summary>
    /// Initializes a tile that can hold samples for the components in
    /// <paramref name="componentSelection"/>. The tile has no dimensions until
    /// <see cref="Reset"/> is called.
    /// </summary>
    /// <param name="header">The JPX header containing component info.</param>
    /// <param name="componentSelection">Indices of the components this tile holds samples for.</param>
    public JpxTile(JpxHeader header, IReadOnlyList<int> componentSelection)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        ComponentSelection = componentSelection ?? throw new ArgumentNullException(nameof(componentSelection));
        ComponentCount = header.ComponentCount;
        ComponentData = new int[ComponentCount][];

        for (int component = 0; component < ComponentCount; component++)
        {
            ComponentData[component] = Array.Empty<int>();
        }
    }

    /// <summary>
    /// Gets the tile header containing metadata for this tile.
    /// </summary>
    public JpxTileHeader? TileHeader { get; private set; }

    /// <summary>
    /// Gets the tile width in pixels.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Gets the tile height in pixels.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Gets the number of samples per component in this tile.
    /// The component buffers may be longer; only this many entries belong to the tile.
    /// </summary>
    public int SampleCount => Width * Height;

    /// <summary>
    /// Gets the number of components in this tile.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the indices of the components this tile holds samples for. Components outside the
    /// selection keep an empty <see cref="ComponentData"/> entry.
    /// </summary>
    public IReadOnlyList<int> ComponentSelection { get; }

    /// <summary>
    /// Gets the decoded component data. Each component is stored as a flat array indexed
    /// by [y * Width + x]. Entries beyond <see cref="SampleCount"/> are spare capacity
    /// left over from a previously decoded, larger tile.
    /// </summary>
    public int[][] ComponentData { get; }

    /// <summary>
    /// Re-targets this tile at another tile of the grid, zeroing its samples.
    /// </summary>
    /// <param name="tileHeader">The tile header containing metadata.</param>
    /// <param name="width">Tile width in pixels.</param>
    /// <param name="height">Tile height in pixels.</param>
    public void Reset(JpxTileHeader tileHeader, int width, int height)
    {
        TileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        Width = width;
        Height = height;

        int sampleCount = width * height;

        for (int index = 0; index < ComponentSelection.Count; index++)
        {
            int component = ComponentSelection[index];

            if (ComponentData[component].Length < sampleCount)
            {
                ComponentData[component] = new int[sampleCount];
            }
            else
            {
                ComponentData[component].AsSpan(0, sampleCount).Clear();
            }
        }
    }

    /// <summary>
    /// Gets the tile index (0-based).
    /// </summary>
    public int TileIndex => TileHeader?.TileIndex ?? 0;

    /// <summary>
    /// Gets the tile X coordinate in the tile grid.
    /// </summary>
    public int TileX => TileHeader?.TileX ?? 0;

    /// <summary>
    /// Gets the tile Y coordinate in the tile grid.
    /// </summary>
    public int TileY => TileHeader?.TileY ?? 0;

    /// <summary>
    /// Returns whether <paramref name="component"/> is part of <see cref="ComponentSelection"/>
    /// and therefore holds reconstructed samples.
    /// </summary>
    public bool IsComponentDecoded(int component)
    {
        for (int index = 0; index < ComponentSelection.Count; index++)
        {
            if (ComponentSelection[index] == component)
            {
                return true;
            }
        }

        return false;
    }
}
