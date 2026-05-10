using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// JPX tile decoder for raw images with zero decomposition levels.
/// Handles the simplest case where pixel data is nearly raw with minimal entropy coding.
/// This is the most common case for simple JPX images in PDF documents.
/// </summary>
internal sealed class JpxRawDecoder : IJpxTileDecoder
{
    private readonly JpxHeader _header;

    public JpxRawDecoder(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxTile DecodeTile(JpxTileHeader tileHeader, ReadOnlySpan<byte> tileData, JpxDecodingParameters decodingParameters)
    {
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        // Create the tile (raw decoder has no DWT levels, so descale has no effect)
        int tileStartX = tileHeader.TileX * (int)_header.TileWidth;
        int tileStartY = tileHeader.TileY * (int)_header.TileHeight;
        int tileWidth = Math.Min((int)_header.TileWidth, (int)_header.Width - tileStartX);
        int tileHeight = Math.Min((int)_header.TileHeight, (int)_header.Height - tileStartY);
        var tile = new JpxTile(_header, tileHeader, tileWidth, tileHeight);

        // Decode raw tile data
        DecodeRawTile(tileData, tile);
        return tile;
    }

    private static void DecodeRawTile(ReadOnlySpan<byte> tileData, JpxTile tile)
    {
        var reader = new JpxSpanReader(tileData);
        
        // Skip SOD marker if present
        if (!reader.EndOfSpan && reader.Remaining >= 2)
        {
            ushort marker = reader.PeekUInt16BE();
            if (marker == JpxMarkers.SOD)
            {
                reader.Skip(2);
            }
        }

        // Extract the remaining raw data
        var rawData = reader.ReadBytes(reader.Remaining);
        
        // Decode each component sequentially
        for (int component = 0; component < tile.ComponentCount; component++)
        {
            var componentData = tile.ComponentData[component];
            int bitDepth = tile.ComponentBitDepths[component];
            bool isSigned = tile.ComponentSigned[component];
            
            DecodeRawComponent(rawData, componentData, bitDepth, isSigned);
        }
    }

    private static void DecodeRawComponent(ReadOnlySpan<byte> data, int[] componentData, int bitDepth, bool isSigned)
    {
        // Create a high-performance pixel reader for this component
        var pixelReader = new JpxPixelReader(data, bitDepth, isSigned); // TODO: [HIGH] well, looks like it's a code duplication.
        
        // Read pixels using the optimized reader
        int pixel = 0;
        while (pixel < componentData.Length && pixelReader.CanRead)
        {
            componentData[pixel++] = pixelReader.ReadPixel();
        }
    }
}