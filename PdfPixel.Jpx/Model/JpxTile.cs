using System;

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
    /// Initializes a tile that can hold samples for the image's components.
    /// The tile has no dimensions until <see cref="Reset"/> is called.
    /// </summary>
    /// <param name="header">The JPX header containing component info.</param>
    public JpxTile(JpxHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

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

        for (int component = 0; component < ComponentCount; component++)
        {
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
    /// Gets the raw signed-or-unsigned component value at the specified coordinates.
    /// </summary>
    public int GetComponentValue(int component, int x, int y)
    {
        if (component < 0 || component >= ComponentCount)
        {
            return 0;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return 0;
        }

        return ComponentData[component][(y * Width) + x];
    }

    /// <summary>
    /// Returns the component value at (x, y) as an unsigned integer normalized to
    /// <paramref name="normalizedBitsPerComponent"/> bits.
    /// Signed samples are biased to unsigned using <paramref name="componentInfo"/>'s
    /// actual precision before any depth rescaling is applied.
    /// </summary>
    public uint GetUnsignedComponentValue(int component, int x, int y, JpxComponent componentInfo, int normalizedBitsPerComponent)
    {
        if (component < 0 || component >= ComponentCount)
        {
            return 0;
        }

        if (componentInfo == null)
        {
            return 0;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return 0;
        }

        int rawValue = ComponentData[component][(y * Width) + x];
        int actualBits = componentInfo.PrecisionBits;

        uint uValue = (componentInfo.IsSigned)
            ? (uint)(rawValue + (1 << (actualBits - 1)))
            : (uint)rawValue;

        int shift = actualBits - normalizedBitsPerComponent;
        if (shift > 0)
        {
            uValue >>= shift;
        }
        else if (shift < 0)
        {
            uValue <<= -shift;
        }

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
        if (component < 0 || component >= ComponentCount)
        {
            return;
        }

        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        ComponentData[component][(y * Width) + x] = value;
    }
}
