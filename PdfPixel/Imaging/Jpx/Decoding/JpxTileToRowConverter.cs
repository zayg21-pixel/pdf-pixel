using PdfPixel.Imaging.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Converts JPX tile-based decoded data into row-based output for PDF processing.
/// Handles horizontal tile assembly and row-by-row streaming.
/// </summary>
internal sealed class JpxTileToRowConverter : IJpxRowProvider
{
    private readonly JpxHeader _header;
    private readonly List<JpxTile> _tiles;
    private readonly int _tilesHorizontal;
    private readonly int _tilesVertical;
    private int _currentRow;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public int ComponentCount { get; }
    public int CurrentRow => _currentRow;

    public JpxTileToRowConverter(JpxHeader header, List<JpxTile> tiles)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        
        Width = (int)header.Width;
        Height = (int)header.Height;
        ComponentCount = header.ComponentCount;
        
        // Calculate tile grid dimensions
        _tilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        _tilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);
        
        _currentRow = 0;
    }

    public bool TryGetNextRow(Span<byte> rowBuffer)
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
        int tileRow = _currentRow / (int)_header.TileHeight;
        int rowWithinTile = _currentRow % (int)_header.TileHeight;

        int outputPixelIndex = 0;

        // Assemble row from horizontal tiles
        for (int tileCol = 0; tileCol < _tilesHorizontal; tileCol++)
        {
            int tileIndex = tileRow * _tilesHorizontal + tileCol;
            
            if (tileIndex >= _tiles.Count)
            {
                // Fill remaining pixels with zeros if tile is missing
                int remainingPixels = Width - outputPixelIndex;
                for (int i = 0; i < remainingPixels * ComponentCount; i++)
                {
                    rowBuffer[outputPixelIndex * ComponentCount + i] = 0;
                }
                break;
            }

            var tile = _tiles[tileIndex];
            if (tile == null || rowWithinTile >= tile.Height)
            {
                // Skip missing or invalid tile
                continue;
            }

            // Calculate how many pixels to copy from this tile
            int tileStartX = tileCol * (int)_header.TileWidth;
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
            // Clean up tile data if needed
            _tiles?.Clear();
            _disposed = true;
        }
    }
}