using System;

namespace PdfPixel.Jpx;

/// <summary>
/// Converts the tiles a <see cref="JpxTileReader"/> reads into image rows, a row of tiles at a time.
/// </summary>
public sealed class JpxTileToRowConverter
{
    private readonly JpxTileReader _tileReader;
    private readonly byte[] _band;
    private readonly int _rowBytes;
    private int _loadedTileRow = -1;
    private int _currentRow;

    /// <summary>
    /// Initializes a converter of the tiles <paramref name="tileReader"/> reads from the codestream of
    /// <paramref name="header"/>.
    /// </summary>
    /// <param name="header">The parsed JPX header.</param>
    /// <param name="tileReader">The reader of the codestream's tiles.</param>
    public JpxTileToRowConverter(JpxHeader header, JpxTileReader tileReader)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        _tileReader = tileReader ?? throw new ArgumentNullException(nameof(tileReader));
        _rowBytes = tileReader.GetRowBytes(tileReader.Width);
        _band = new byte[_rowBytes * Math.Min(tileReader.TileHeight, tileReader.Height)];

        int componentCount = tileReader.ComponentCount;
        int opacityComponentIndex = header.OpacityComponentIndex;

        HasAlphaChannel = opacityComponentIndex >= 0
            && componentCount > 0
            && tileReader.ComponentSelection[componentCount - 1] == opacityComponentIndex;
        ColorComponentCount = componentCount - (HasAlphaChannel ? 1 : 0);
    }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int Width => _tileReader.Width;

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int Height => _tileReader.Height;

    /// <summary>
    /// Gets the total number of components per pixel (including alpha when present).
    /// </summary>
    public int ComponentCount => _tileReader.ComponentCount;

    /// <summary>
    /// Gets the number of color (non-alpha) components per pixel.
    /// </summary>
    public int ColorComponentCount { get; }

    /// <summary>
    /// Gets a value indicating whether the output rows contain an alpha channel
    /// as the last component.
    /// </summary>
    public bool HasAlphaChannel { get; }

    /// <summary>
    /// Gets the bit depth per component as declared in the JPX header (capped at 16).
    /// Callers must use this value rather than the PDF /BitsPerComponent entry.
    /// </summary>
    public int BitsPerComponent => _tileReader.BitsPerComponent;

    /// <summary>
    /// Gets the current row index (0-based).
    /// </summary>
    public int CurrentRow => _currentRow;

    /// <summary>
    /// Attempts to read the next row of image data as bit-packed samples at <see cref="BitsPerComponent"/> depth.
    /// </summary>
    /// <param name="rowBuffer">Buffer to receive the row data. Must be at least <c>(Width * ComponentCount * BitsPerComponent + 7) / 8</c> bytes.</param>
    /// <param name="observer">Observer to notify on long-running operation.</param>
    /// <returns>True if a row was successfully read, false if at end of image.</returns>
    public bool TryGetNextRow(in Span<byte> rowBuffer, IJpxExecutionObserver? observer = default)
    {
        if (_currentRow >= Height)
        {
            return false;
        }

        if (rowBuffer.Length < _rowBytes)
        {
            throw new ArgumentException($"Row buffer too small. Required: {_rowBytes}, provided: {rowBuffer.Length}");
        }

        int tileRow = _currentRow / _tileReader.TileHeight;

        EnsureTileRowLoaded(tileRow, observer);

        int rowWithinTile = _currentRow - (tileRow * _tileReader.TileHeight);

        _band.AsSpan(rowWithinTile * _rowBytes, _rowBytes).CopyTo(rowBuffer);
        _currentRow++;

        return true;
    }

    /// <summary>
    /// Reads every tile of <paramref name="tileRow"/> into the band, unless it already holds them.
    /// </summary>
    private void EnsureTileRowLoaded(int tileRow, IJpxExecutionObserver? observer)
    {
        if (_loadedTileRow == tileRow)
        {
            return;
        }

        Array.Clear(_band, 0, _band.Length);

        for (int tileColumn = 0; tileColumn < _tileReader.TilesHorizontal; tileColumn++)
        {
            observer?.Notify();

            int tileIndex = (tileRow * _tileReader.TilesHorizontal) + tileColumn;

            _tileReader.ReadTile(tileIndex, _band, _rowBytes, tileColumn * _tileReader.TileWidth, observer);
        }

        _loadedTileRow = tileRow;
    }
}
