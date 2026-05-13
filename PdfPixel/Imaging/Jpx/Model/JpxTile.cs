using System;

namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Represents a decoded JPX tile with component data.
/// </summary>
internal sealed class JpxTile
{
    /// <summary>
    /// Gets the tile header containing metadata for this tile.
    /// </summary>
    public JpxTileHeader TileHeader { get; }

    /// <summary>
    /// Gets the tile width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the tile height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of components in this tile.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the decoded component data. Each component is stored as a 2D array [component][y * width + x].
    /// This provides efficient access while maintaining the logical 2D structure.
    /// </summary>
    public int[][] ComponentData { get; }

    /// <summary>
    /// Initializes a new JPX tile with the specified dimensions.
    /// </summary>
    /// <param name="header">The JPX header containing component info.</param>
    /// <param name="tileHeader">The tile header containing metadata.</param>
    /// <param name="width">Tile width in pixels.</param>
    /// <param name="height">Tile height in pixels.</param>
    public JpxTile(JpxHeader header, JpxTileHeader tileHeader, int width, int height)
    {
        TileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        ComponentCount = header.ComponentCount;
        Width = width;
        Height = height;

        // Initialize component data arrays
        ComponentData = new int[ComponentCount][];
        for (int i = 0; i < ComponentCount; i++)
        {
            ComponentData[i] = new int[Width * Height];
        }
    }

    /// <summary>
    /// Gets the tile index (0-based).
    /// </summary>
    public int TileIndex => TileHeader.TileIndex;

    /// <summary>
    /// Gets the tile X coordinate in the tile grid.
    /// </summary>
    public int TileX => TileHeader.TileX;

    /// <summary>
    /// Gets the tile Y coordinate in the tile grid.
    /// </summary>
    public int TileY => TileHeader.TileY;

    /// <summary>
    /// Gets the raw signed-or-unsigned component value at the specified coordinates.
    /// </summary>
    public int GetComponentValue(int component, int x, int y)
    {
        if (component < 0 || component >= ComponentCount || ComponentData[component] == null)
        {
            return 0;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return 0;
        }

        return ComponentData[component][y * Width + x];
    }

    /// <summary>
    /// Returns the component value at (x, y) as an unsigned integer normalized to
    /// <paramref name="normalizedBitsPerComponent"/> bits.
    /// Signed samples are biased to unsigned using <paramref name="componentInfo"/>'s
    /// actual precision before any depth rescaling is applied.
    /// </summary>
    public uint GetUnsignedComponentValue(int component, int x, int y, JpxComponent componentInfo, int normalizedBitsPerComponent)
    {
        if (component < 0 || component >= ComponentCount || ComponentData[component] == null)
        {
            return 0;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return 0;
        }

        int rawValue = ComponentData[component][y * Width + x];
        int actualBits = componentInfo.PrecisionBits;

        uint uValue = componentInfo.IsSigned
            ? (uint)(rawValue + (1 << (actualBits - 1)))
            : (uint)rawValue;

        int shift = actualBits - normalizedBitsPerComponent;
        if (shift > 0)
            uValue >>= shift;
        else if (shift < 0)
            uValue <<= -shift;

        return uValue;
    }

    /// <summary>
    /// Sets the component value at the specified coordinates.
    /// </summary>
    /// <param name="component">Component index (0-based).</param>
    /// <param name="x">X coordinate within tile.</param>
    /// <param name="y">Y coordinate within tile.</param>
    /// <param name="value">Value to set.</param>
    public void SetComponentValue(int component, int x, int y, int value)
    {
        if (component < 0 || component >= ComponentCount || ComponentData[component] == null)
        {
            return;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        ComponentData[component][y * Width + x] = value;
    }
}