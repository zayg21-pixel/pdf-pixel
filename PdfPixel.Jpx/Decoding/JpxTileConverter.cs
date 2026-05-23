using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Converts a single decoded <see cref="JpxTile"/> into a row-by-row bit-packed stream.
/// Use this instead of <see cref="JpxTileToRowConverter"/> when the image is known to
/// consist of exactly one tile.
/// </summary>
internal sealed class JpxTileConverter
{
    private readonly JpxTile _tile;
    private readonly JpxHeader _header;
    private int _currentRow;

    public JpxTileConverter(JpxTile tile, JpxHeader header)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        _header = header ?? throw new ArgumentNullException(nameof(header));

        Width = tile.Width;
        Height = tile.Height;
        ComponentCount = tile.ComponentCount;

        int headerBpc = header.Components.Count > 0 ? header.Components[0].PrecisionBits : 8;
        BitsPerComponent = Math.Min(16, Math.Max(1, headerBpc));
    }

    public int Width { get; }
    public int Height { get; }
    public int ComponentCount { get; }

    /// <summary>
    /// Bit depth per component, normalized from the JPX header (capped at 16).
    /// All components are rescaled to this depth by <see cref="TryGetNextRow"/>.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Writes the next image row as bit-packed samples into <paramref name="rowBuffer"/>.
    /// Returns false when all rows have been consumed.
    /// The buffer must be at least <c>(Width * ComponentCount * BitsPerComponent + 7) / 8</c> bytes.
    /// </summary>
    public bool TryGetNextRow(Span<byte> rowBuffer)
    {
        if (_currentRow >= Height)
        {
            return false;
        }

        int requiredBytes = (Width * ComponentCount * BitsPerComponent + 7) / 8;
        if (rowBuffer.Length < requiredBytes)
        {
            throw new ArgumentException($"Row buffer too small. Required: {requiredBytes}, provided: {rowBuffer.Length}");
        }

        rowBuffer.Slice(0, requiredBytes).Clear();

        var writer = new JpxBitWriter(rowBuffer);

        for (int x = 0; x < Width; x++)
        {
            for (int component = 0; component < ComponentCount; component++)
            {
                uint uValue = _tile.GetUnsignedComponentValue(component, x, _currentRow, _header.Components[component], BitsPerComponent);
                writer.WriteBits(BitsPerComponent, uValue);
            }
        }

        _currentRow++;
        return true;
    }
}
