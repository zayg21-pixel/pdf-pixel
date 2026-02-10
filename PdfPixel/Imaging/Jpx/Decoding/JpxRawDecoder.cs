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

    public JpxTile DecodeTile(JpxTileHeader tileHeader, ReadOnlySpan<byte> tileData)
    {
        if (tileHeader == null)
        {
            throw new ArgumentNullException(nameof(tileHeader));
        }

        // Create the tile
        var tile = new JpxTile(_header, tileHeader);

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
        var pixelReader = new JpxPixelReader(data, bitDepth, isSigned);
        
        // Read pixels using the optimized reader
        int pixel = 0;
        try
        {
            while (pixel < componentData.Length && pixelReader.CanRead)
            {
                componentData[pixel++] = pixelReader.ReadPixel();
            }
        }
        catch (Exception)
        {
            // Fill remainder with appropriate default value if reading fails
            FillRemainingPixels(componentData, pixel, bitDepth, isSigned);
        }
    }

    private static void FillRemainingPixels(int[] componentData, int startPixel, int bitDepth, bool isSigned)
    {
        // Fill remainder with mid-gray value
        int fillValue = isSigned ? 0 : (1 << Math.Min(bitDepth, 31)) / 2;
        
        for (int i = startPixel; i < componentData.Length; i++)
        {
            componentData[i] = fillValue;
        }
    }
}