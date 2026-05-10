using PdfPixel.Imaging.Jpx.Model;
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
    /// Gets the current row index (0-based).
    /// </summary>
    public int CurrentRow => _currentRow;

    /// <summary>
    /// Attempts to read the next row of image data.
    /// </summary>
    /// <param name="rowBuffer">Buffer to receive the row data. Must be at least Width * ComponentCount bytes.</param>
    /// <param name="cancellationToken">Token to cancel tile decoding between tiles.</param>
    /// <returns>True if a row was successfully read, false if at end of image.</returns>
    public bool TryGetNextRow(Span<byte> rowBuffer, CancellationToken cancellationToken = default)
    {
        if (_disposed || _currentRow >= Height)
        {
            return false;
        }

        if (rowBuffer.Length < Width * ComponentCount)
        {
            throw new ArgumentException($"Row buffer too small. Required: {Width * ComponentCount}, provided: {rowBuffer.Length}");
        }

        // Determine which tile row this image row belongs to
        int tileRow = _currentRow / _reducedTileHeight;
        int rowWithinTile = _currentRow % _reducedTileHeight;

        // Decode this tile row lazily if we haven't already
        EnsureTileRowLoaded(tileRow, cancellationToken);

        int outputPixelIndex = 0;

        // Assemble row from horizontal tiles
        for (int tileCol = 0; tileCol < _tileProvider.TilesHorizontal; tileCol++)
        {
            int tileIndex = tileRow * _tileProvider.TilesHorizontal + tileCol;

            if (tileIndex >= _tileProvider.TotalTiles)
            {
                // Fill remaining pixels with zeros if tile is missing
                int remainingPixels = Width - outputPixelIndex;
                for (int i = 0; i < remainingPixels * ComponentCount; i++)
                {
                    rowBuffer[outputPixelIndex * ComponentCount + i] = 0;
                }
                break;
            }

            var tile = _currentTileRowTiles[tileCol];
            if (tile == null || rowWithinTile >= tile.Height)
            {
                // Skip missing or invalid tile
                continue;
            }

            // Calculate how many pixels to copy from this tile
            int tileStartX = tileCol * _reducedTileWidth;
            int pixelsFromTile = Math.Min(tile.Width, Width - tileStartX);

            // Copy pixels from tile to row buffer
            for (int pixelInTile = 0; pixelInTile < pixelsFromTile; pixelInTile++)
            {
                if (outputPixelIndex >= Width)
                {
                    break; // Don't exceed image width
                }

                // Copy each component for this pixel
                for (int component = 0; component < ComponentCount && component < tile.ComponentCount; component++)
                {
                    if (tile.ComponentData[component] != null)
                    {
                        // Use the tile's helper method to get the component value
                        int componentValue = tile.GetComponentValue(component, pixelInTile, rowWithinTile);
                        
                        // Apply bit depth conversion and clamping
                        byte byteValue = ConvertComponentToByte(componentValue, tile.ComponentBitDepths[component], tile.ComponentSigned[component]);
                        
                        rowBuffer[outputPixelIndex * ComponentCount + component] = byteValue;
                    }
                    else
                    {
                        rowBuffer[outputPixelIndex * ComponentCount + component] = 0;
                    }
                }

                outputPixelIndex++;
            }
        }

        _currentRow++;
        return true;
    }

    private static byte ConvertComponentToByte(int value, int bitDepth, bool isSigned)
    {
        if (bitDepth <= 8)
        {
            // For 8 bits or less, scale to full byte range
            if (isSigned)
            {
                // Signed: map from [-2^(n-1), 2^(n-1)-1] to [0, 255]
                int minVal = -(1 << (bitDepth - 1));
                int maxVal = (1 << (bitDepth - 1)) - 1;
                value = Math.Max(minVal, Math.Min(maxVal, value));
                return (byte)((value - minVal) * 255 / (maxVal - minVal));
            }
            else
            {
                // Unsigned: map from [0, 2^n-1] to [0, 255]
                int maxVal = (1 << bitDepth) - 1;
                value = Math.Max(0, Math.Min(maxVal, value));
                return (byte)(value * 255 / maxVal);
            }
        }
        else
        {
            // For more than 8 bits, take the most significant bits
            int shift = bitDepth - 8;
            if (isSigned)
            {
                // Handle signed values
                value = Math.Max(-(1 << (bitDepth - 1)), Math.Min((1 << (bitDepth - 1)) - 1, value));
                value = (value >> shift) + 128; // Shift to unsigned range
            }
            else
            {
                value = Math.Max(0, Math.Min((1 << bitDepth) - 1, value));
                value = value >> shift;
            }
            
            return (byte)Math.Max(0, Math.Min(255, value));
        }
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