using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Parsing;
using System;
using System.Threading;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Converts JPX tile-based decoded data into row-based output for PDF processing.
/// Handles horizontal tile assembly and row-by-row streaming.
/// Tiles are decoded lazily: a tile row is decoded only when the first image row
/// belonging to that tile row is requested, and released when no longer needed.
/// </summary>
internal sealed class JpxTileToRowConverter : IDisposable
{
    private readonly JpxHeader _header;
    private readonly JpxTileProvider _tileProvider;
    private readonly JpxDecodingParameters _decodingParameters;
    private int _currentRow;
    private bool _disposed;

    /// <summary>
    /// Reduced tile width used for row-to-tile mapping.
    /// </summary>
    private readonly int _reducedTileWidth;

    /// <summary>
    /// Reduced tile height used for row-to-tile mapping.
    /// </summary>
    private readonly int _reducedTileHeight;

    /// <summary>
    /// Cached decoded tiles for the current tile row. Indexed by tile column.
    /// </summary>
    private JpxTile[] _currentTileRowTiles;

    /// <summary>
    /// The tile row index that <see cref="_currentTileRowTiles"/> corresponds to, or -1 if none.
    /// </summary>
    private int _loadedTileRow = -1;

    /// <summary>
    /// Initializes a new <see cref="JpxTileToRowConverter"/> with a tile provider for lazy decoding.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image metadata.</param>
    /// <param name="tileProvider">Provider that decodes tiles on demand.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    public JpxTileToRowConverter(JpxHeader header, JpxTileProvider tileProvider, JpxDecodingParameters decodingParameters = default)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tileProvider = tileProvider;
        _decodingParameters = decodingParameters.DescaleFactor >= 1 ? decodingParameters : JpxDecodingParameters.Default;

        Width = _decodingParameters.ReduceDimension((int)header.Width);
        Height = _decodingParameters.ReduceDimension((int)header.Height);
        ComponentCount = header.ComponentCount;

        _reducedTileWidth = _decodingParameters.ReduceDimension((int)header.TileWidth);
        _reducedTileHeight = _decodingParameters.ReduceDimension((int)header.TileHeight);

        _currentRow = 0;
        _currentTileRowTiles = new JpxTile[_tileProvider.TilesHorizontal];

        // Use the JPX header's precision (per spec, ignore PDF BitsPerComponent for JPX).
        // Cap at 16, the maximum supported by the row processor.
        int headerBpc = header.Components.Count > 0 ? header.Components[0].PrecisionBits : 8;
        BitsPerComponent = Math.Min(16, Math.Max(1, headerBpc));
    }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of components per pixel.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the bit depth per component as declared in the JPX header (capped at 16).
    /// Callers must use this value rather than the PDF /BitsPerComponent entry.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Gets the current row index (0-based).
    /// </summary>
    public int CurrentRow => _currentRow;

    /// <summary>
    /// Attempts to read the next row of image data as bit-packed samples at <see cref="BitsPerComponent"/> depth.
    /// </summary>
    /// <param name="rowBuffer">Buffer to receive the row data. Must be at least <c>(Width * ComponentCount * BitsPerComponent + 7) / 8</c> bytes.</param>
    /// <param name="cancellationToken">Token to cancel tile decoding between tiles.</param>
    /// <returns>True if a row was successfully read, false if at end of image.</returns>
    public bool TryGetNextRow(Span<byte> rowBuffer, CancellationToken cancellationToken = default)
    {
        if (_disposed || _currentRow >= Height)
        {
            return false;
        }

        int requiredBytes = (Width * ComponentCount * BitsPerComponent + 7) / 8;
        if (rowBuffer.Length < requiredBytes)
        {
            throw new ArgumentException($"Row buffer too small. Required: {requiredBytes}, provided: {rowBuffer.Length}");
        }

        rowBuffer.Slice(0, requiredBytes).Clear();

        int tileRow = _currentRow / _reducedTileHeight;
        int rowWithinTile = _currentRow % _reducedTileHeight;

        EnsureTileRowLoaded(tileRow, cancellationToken);

        var writer = new UintBitWriter(rowBuffer);
        int outputPixelIndex = 0;

        for (int tileCol = 0; tileCol < _tileProvider.TilesHorizontal; tileCol++)
        {
            int tileIndex = tileRow * _tileProvider.TilesHorizontal + tileCol;

            if (tileIndex >= _tileProvider.TotalTiles)
            {
                int remainingPixels = Width - outputPixelIndex;
                for (int i = 0; i < remainingPixels * ComponentCount; i++)
                    writer.WriteBits(BitsPerComponent, 0);
                break;
            }

            var tile = _currentTileRowTiles[tileCol];
            if (tile == null || rowWithinTile >= tile.Height)
            {
                int tileStartX = tileCol * _reducedTileWidth;
                int missingPixels = Math.Min(_reducedTileWidth, Width - tileStartX);
                for (int i = 0; i < missingPixels * ComponentCount; i++)
                    writer.WriteBits(BitsPerComponent, 0);
                outputPixelIndex += missingPixels;
                continue;
            }

            int tileStartXPixel = tileCol * _reducedTileWidth;
            int pixelsFromTile = Math.Min(tile.Width, Width - tileStartXPixel);

            for (int pixelInTile = 0; pixelInTile < pixelsFromTile; pixelInTile++)
            {
                if (outputPixelIndex >= Width)
                    break;

                for (int component = 0; component < ComponentCount; component++)
                {
                    uint uValue = 0;
                    if (component < tile.ComponentCount && tile.ComponentData[component] != null)
                    {
                        uValue = tile.GetUnsignedComponentValue(component, pixelInTile, rowWithinTile, _header.Components[component], BitsPerComponent);
                    }
                    writer.WriteBits(BitsPerComponent, uValue);
                }

                outputPixelIndex++;
            }
        }

        _currentRow++;
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _currentTileRowTiles = null;
            _disposed = true;
        }
    }

    /// <summary>
    /// Ensures that the tiles for the given tile row are decoded and cached.
    /// Releases the previous tile row when advancing to a new one.
    /// </summary>
    private void EnsureTileRowLoaded(int tileRow, CancellationToken cancellationToken)
    {
        if (_loadedTileRow == tileRow)
        {
            return;
        }

        // Decode each tile in this row
        for (int tileCol = 0; tileCol < _tileProvider.TilesHorizontal; tileCol++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int tileIndex = tileRow * _tileProvider.TilesHorizontal + tileCol;

            if (tileIndex < _tileProvider.TotalTiles)
            {
                _currentTileRowTiles[tileCol] = _tileProvider.DecodeTile(tileIndex);
            }
            else
            {
                _currentTileRowTiles[tileCol] = null;
            }
        }

        _loadedTileRow = tileRow;
    }
}