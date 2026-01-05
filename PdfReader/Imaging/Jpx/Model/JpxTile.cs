using System;

namespace PdfReader.Imaging.Jpx.Model;

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
    /// Gets the bit depth for each component.
    /// </summary>
    public int[] ComponentBitDepths { get; }

    /// <summary>
    /// Gets whether each component is signed.
    /// </summary>
    public bool[] ComponentSigned { get; }

    /// <summary>
    /// Initializes a new JPX tile with the specified parameters.
    /// Calculates tile dimensions automatically based on header and tile position.
    /// </summary>
    /// <param name="header">The JPX header containing image dimensions and component info.</param>
    /// <param name="tileHeader">The tile header containing metadata.</param>
    public JpxTile(JpxHeader header, JpxTileHeader tileHeader)
    {
        TileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        ComponentCount = header.ComponentCount;

        // Calculate tile dimensions based on position in grid
        int tileStartX = tileHeader.TileX * (int)header.TileWidth;
        int tileStartY = tileHeader.TileY * (int)header.TileHeight;
        
        Width = Math.Min((int)header.TileWidth, (int)header.Width - tileStartX);
        Height = Math.Min((int)header.TileHeight, (int)header.Height - tileStartY);

        // Initialize component metadata arrays
        ComponentBitDepths = new int[ComponentCount];
        ComponentSigned = new bool[ComponentCount];
        
        for (int i = 0; i < ComponentCount; i++)
        {
            ComponentBitDepths[i] = header.Components[i].PrecisionBits;
            ComponentSigned[i] = header.Components[i].IsSigned;
        }

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
    /// Gets the component value at the specified coordinates.
    /// </summary>
    /// <param name="component">Component index (0-based).</param>
    /// <param name="x">X coordinate within tile.</param>
    /// <param name="y">Y coordinate within tile.</param>
    /// <returns>Component value at the specified position.</returns>
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