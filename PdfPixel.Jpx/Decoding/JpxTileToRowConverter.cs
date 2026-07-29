using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Converts JPX tile-based decoded data into row-based output for PDF processing.
/// Handles horizontal tile assembly and row-by-row streaming.
/// Tiles are decoded lazily: a tile row is decoded only when the first image row
/// belonging to that tile row is requested, and released when no longer needed.
/// </summary>
public sealed class JpxTileToRowConverter
{
    private readonly JpxHeader _header;
    private readonly JpxTileProvider _tileProvider;
    private readonly JpxDecodingParameters _decodingParameters;
    private int _currentRow;

    /// <summary>
    /// Reduced tile width used for row-to-tile mapping.
    /// </summary>
    private readonly int _reducedTileWidth;

    /// <summary>
    /// Reduced tile height used for row-to-tile mapping.
    /// </summary>
    private readonly int _reducedTileHeight;

    /// <summary>
    /// Reusable tile storage, one per tile column. Loading a tile row refills these in place
    /// rather than allocating sample buffers for every tile of the grid.
    /// </summary>
    private readonly JpxTile[] _tileBuffers;

    /// <summary>
    /// Whether the tile buffer for the matching column holds decoded data for the current
    /// tile row. Columns left undecoded produce zeroed output samples.
    /// </summary>
    private readonly bool[] _tileDecoded;

    /// <summary>
    /// The tile row index the tile buffers currently hold, or -1 if none.
    /// </summary>
    private int _loadedTileRow = -1;

    /// <summary>
    /// Indices of color (non-alpha) components in output order.
    /// </summary>
    private readonly int[] _colorComponentIndices;

    /// <summary>
    /// Index of the alpha component in the tile's ComponentData, or -1 when no alpha is present.
    /// </summary>
    private readonly int _alphaComponentIndex;

    /// <summary>
    /// Initializes a new <see cref="JpxTileToRowConverter"/> with a tile provider for lazy decoding.
    /// </summary>
    /// <param name="header">Parsed JPX header containing image metadata.</param>
    /// <param name="tileProvider">Provider that decodes tiles on demand.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    public JpxTileToRowConverter(JpxHeader header, in JpxTileProvider tileProvider, in JpxDecodingParameters decodingParameters = default)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tileProvider = tileProvider;
        _decodingParameters = (decodingParameters.DescaleFactor >= 1) ? decodingParameters : JpxDecodingParameters.Default;

        // TODO: [HIGH] Account for the reference grid origin. The image spans Xsiz - XOsiz by
        // Ysiz - YOsiz, but these take Xsiz and Ysiz whole, so an image whose origin is away from
        // zero comes out too large by the origin and its samples land at the wrong offset. The
        // tile grid needs the same treatment, since a tile starts at XTOsiz + index * XTsiz.
        // Covered by the failing baboon-offset-image and baboon-offset-tile corpus files.
        Width = _decodingParameters.ReduceDimension((int)header.Width);
        Height = _decodingParameters.ReduceDimension((int)header.Height);

        _alphaComponentIndex = header.OpacityComponentIndex;

        if (_alphaComponentIndex >= 0)
        {
            _colorComponentIndices = new int[header.ComponentCount - 1];
            int colorIndex = 0;
            for (int i = 0; i < header.ComponentCount; i++)
            {
                if (i != _alphaComponentIndex)
                {
                    _colorComponentIndices[colorIndex++] = i;
                }
            }

            ColorComponentCount = _colorComponentIndices.Length;
        }
        else
        {
            _colorComponentIndices = new int[header.ComponentCount];
            for (int i = 0; i < header.ComponentCount; i++)
            {
                _colorComponentIndices[i] = i;
            }

            ColorComponentCount = header.ComponentCount;
        }

        ComponentCount = header.ComponentCount;

        _reducedTileWidth = _decodingParameters.ReduceDimension((int)header.TileWidth);
        _reducedTileHeight = _decodingParameters.ReduceDimension((int)header.TileHeight);

        _currentRow = 0;
        _tileBuffers = new JpxTile[_tileProvider.TilesHorizontal];
        _tileDecoded = new bool[_tileProvider.TilesHorizontal];

        for (int tileColumn = 0; tileColumn < _tileBuffers.Length; tileColumn++)
        {
            _tileBuffers[tileColumn] = tileProvider.CreateTile();
        }

        // Use the JPX header's precision (per spec, ignore PDF BitsPerComponent for JPX).
        int headerBpc = 0;
        for (int i = 0; i < header.Components.Count; i++)
        {
            headerBpc = Math.Max(headerBpc, header.Components[i].PrecisionBits);
        }

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
    /// Gets the total number of components per pixel (including alpha when present).
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Gets the number of color (non-alpha) components per pixel.
    /// </summary>
    public int ColorComponentCount { get; }

    /// <summary>
    /// Gets a value indicating whether the output rows contain an alpha channel
    /// as the last component.
    /// </summary>
    public bool HasAlphaChannel => _alphaComponentIndex >= 0;

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
    /// <param name="observer">Observer to notify on long-running operation.</param>
    /// <returns>True if a row was successfully read, false if at end of image.</returns>
    public bool TryGetNextRow(in Span<byte> rowBuffer, IJpxExectionObserver? observer = default)
    {
        if (_currentRow >= Height)
        {
            return false;
        }

        int requiredBytes = ((Width * ComponentCount * BitsPerComponent) + 7) / 8;
        if (rowBuffer.Length < requiredBytes)
        {
            throw new ArgumentException($"Row buffer too small. Required: {requiredBytes}, provided: {rowBuffer.Length}");
        }

        rowBuffer.Slice(0, requiredBytes).Clear();

        int tileRow = _currentRow / _reducedTileHeight;
        int rowWithinTile = _currentRow % _reducedTileHeight;

        EnsureTileRowLoaded(tileRow, observer);

        int outputPixelIndex = 0;

        WriteRowBits(rowBuffer, tileRow, rowWithinTile, ref outputPixelIndex);

        _currentRow++;
        return true;
    }

    private void WriteRowBits(in Span<byte> rowBuffer, int tileRow, int rowWithinTile, ref int outputPixelIndex)
    {
        JpxBitWriter writer = new(rowBuffer);

        for (int tileCol = 0; tileCol < _tileProvider.TilesHorizontal; tileCol++)
        {
            int tileIndex = (tileRow * _tileProvider.TilesHorizontal) + tileCol;

            if (tileIndex >= _tileProvider.TotalTiles)
            {
                int remainingPixels = Width - outputPixelIndex;
                for (int i = 0; i < remainingPixels * ComponentCount; i++)
                {
                    writer.WriteBits(BitsPerComponent, 0);
                }

                break;
            }

            JpxTile tile = _tileBuffers[tileCol];
            if (!_tileDecoded[tileCol] || rowWithinTile >= tile.Height)
            {
                int tileStartX = tileCol * _reducedTileWidth;
                int missingPixels = Math.Min(_reducedTileWidth, Width - tileStartX);
                for (int i = 0; i < missingPixels * ComponentCount; i++)
                {
                    writer.WriteBits(BitsPerComponent, 0);
                }

                outputPixelIndex += missingPixels;
                continue;
            }

            int tileStartXPixel = tileCol * _reducedTileWidth;
            int pixelsFromTile = Math.Min(tile.Width, Width - tileStartXPixel);

            for (int pixelInTile = 0; pixelInTile < pixelsFromTile; pixelInTile++)
            {
                if (outputPixelIndex >= Width)
                {
                    break;
                }

                for (int i = 0; i < _colorComponentIndices.Length; i++)
                {
                    int component = _colorComponentIndices[i];
                    uint uValue = 0;
                    if (component < tile.ComponentCount && tile.ComponentData[component] != null)
                    {
                        uValue = tile.GetUnsignedComponentValue(component, pixelInTile, rowWithinTile, _header.Components[component], BitsPerComponent);
                    }

                    writer.WriteBits(BitsPerComponent, uValue);
                }

                if (_alphaComponentIndex >= 0)
                {
                    uint alphaValue = 0;
                    if (_alphaComponentIndex < tile.ComponentCount && tile.ComponentData[_alphaComponentIndex] != null)
                    {
                        alphaValue = tile.GetUnsignedComponentValue(_alphaComponentIndex, pixelInTile, rowWithinTile, _header.Components[_alphaComponentIndex], BitsPerComponent);
                    }

                    writer.WriteBits(BitsPerComponent, alphaValue);
                }

                outputPixelIndex++;
            }
        }

        writer.Flush();
    }

    /// <summary>
    /// Ensures that the tiles for the given tile row are decoded and cached.
    /// Releases the previous tile row when advancing to a new one.
    /// </summary>
    private void EnsureTileRowLoaded(int tileRow, IJpxExectionObserver? observer)
    {
        if (_loadedTileRow == tileRow)
        {
            return;
        }

        // Decode each tile in this row
        for (int tileCol = 0; tileCol < _tileProvider.TilesHorizontal; tileCol++)
        {
            observer?.Notify();

            int tileIndex = (tileRow * _tileProvider.TilesHorizontal) + tileCol;

            bool shouldDecode = tileIndex < _tileProvider.TotalTiles && ShouldDecodeTile(tileIndex);

            if (shouldDecode)
            {
                _tileProvider.DecodeTile(tileIndex, _tileBuffers[tileCol], observer);
            }

            _tileDecoded[tileCol] = shouldDecode;
        }

        _loadedTileRow = tileRow;
    }

    /// <summary>
    /// Determines whether the tile at <paramref name="tileIndex"/> overlaps any of the
    /// <see cref="JpxDecodingParameters.RegionsOfInterest"/>. Tiles outside every region
    /// are left undecoded — <see cref="TryGetNextRow"/> already produces zeroed placeholder
    /// samples for rows whose tile is null.
    /// </summary>
    private bool ShouldDecodeTile(int tileIndex)
    {
        IReadOnlyList<JpxRectangle>? regionsOfInterest = _decodingParameters.RegionsOfInterest;
        if (regionsOfInterest == null)
        {
            return true;
        }

        int tileColumn = tileIndex % _tileProvider.TilesHorizontal;
        int tileRow = tileIndex / _tileProvider.TilesHorizontal;
        int tileStartX = tileColumn * (int)_header.TileWidth;
        int tileStartY = tileRow * (int)_header.TileHeight;
        int tileWidth = Math.Min((int)_header.TileWidth, (int)_header.Width - tileStartX);
        int tileHeight = Math.Min((int)_header.TileHeight, (int)_header.Height - tileStartY);
        JpxRectangle tileBounds = new(tileStartX, tileStartY, tileWidth, tileHeight);

        for (int i = 0; i < regionsOfInterest.Count; i++)
        {
            if (tileBounds.IntersectsWith(regionsOfInterest[i]))
            {
                return true;
            }
        }

        return false;
    }
}
