using PdfReader.Imaging.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfReader.Imaging.Jpx.Parsing;

/// <summary>
/// Common packet parsing engine for JPEG 2000 (JPX) packets.
/// Handles the actual parsing logic while progression order parsers provide enumeration order.
/// </summary>
internal sealed class JpxPacketParsingEngine
{
    private readonly JpxHeader _header;
    private readonly JpxTileHeader _tileHeader;
    private readonly JpxPacketHeaderParser _headerParser;

    public JpxPacketParsingEngine(JpxHeader header, JpxTileHeader tileHeader)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        _headerParser = new JpxPacketHeaderParser(header, tileHeader);
    }

    /// <summary>
    /// Parses packets according to the provided enumeration order.
    /// </summary>
    /// <param name="packetData">Raw packet data to parse.</param>
    /// <param name="enumerationOrder">Enumeration order for packets (layer, resolution, component, precinct coordinates).</param>
    /// <returns>Array of parsed packets.</returns>
    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, IEnumerable<PacketCoordinate> enumerationOrder)
    {
        if (packetData.Length == 0)
        {
            return Array.Empty<JpxPacket>();
        }

        ValidateHeader();

        var packets = new List<JpxPacket>();
        var bitReader = new JpxBitReader(packetData);

        foreach (var coordinate in enumerationOrder)
        {
            var packet = ParseSinglePacket(ref bitReader, coordinate);
            packets.Add(packet);
        }

        return packets.ToArray();
    }

    /// <summary>
    /// Parses a single packet at the given coordinates.
    /// </summary>
    private JpxPacket ParseSinglePacket(ref JpxBitReader bitReader, PacketCoordinate coordinate)
    {
        // Parse packet header
        var headerInfo = _headerParser.ParsePacketHeader(
            ref bitReader,
            coordinate.Layer,
            coordinate.Resolution,
            coordinate.Component,
            coordinate.PrecinctX,
            coordinate.PrecinctY);

        JpxCodeBlock[] codeBlocks;
        if (!headerInfo.IsEmpty)
        {
            // Parse packet body to extract code-block data
            codeBlocks = ParsePacketBody(ref bitReader, headerInfo.CodeBlocks);
        }
        else
        {
            codeBlocks = Array.Empty<JpxCodeBlock>();
        }

        return new JpxPacket
        {
            Layer = coordinate.Layer,
            Resolution = coordinate.Resolution,
            Component = coordinate.Component,
            PrecinctX = coordinate.PrecinctX,
            PrecinctY = coordinate.PrecinctY,
            CodeBlocks = codeBlocks
        };
    }

    /// <summary>
    /// Parses the packet body to extract raw code-block data.
    /// </summary>
    private JpxCodeBlock[] ParsePacketBody(ref JpxBitReader bitReader, JpxCodeBlock[] headerCodeBlocks)
    {
        var codeBlocks = new JpxCodeBlock[headerCodeBlocks.Length];

        for (int i = 0; i < headerCodeBlocks.Length; i++)
        {
            var headerBlock = headerCodeBlocks[i];
            var codeBlock = new JpxCodeBlock
            {
                X = headerBlock.X,
                Y = headerBlock.Y,
                Width = _header.CodingStyle?.CodeBlockWidth ?? 64,
                Height = _header.CodingStyle?.CodeBlockHeight ?? 64,
                ZeroBitPlanes = headerBlock.ZeroBitPlanes,
                CodingPasses = headerBlock.CodingPasses,
                DataLength = headerBlock.DataLength
            };

            // Use actual length from header instead of estimation
            int dataLength = headerBlock.DataLength;
            
            if (dataLength > 0 && bitReader.Remaining >= dataLength)
            {
                // Align to byte boundary for code-block data
                bitReader.ByteAlign();
                
                // Read the exact number of bytes specified in the header
                var data = new byte[dataLength];
                for (int j = 0; j < dataLength && bitReader.HasMoreData; j++)
                {
                    data[j] = (byte)bitReader.ReadBits(8);
                }
                codeBlock.Data = data;
            }
            else
            {
                codeBlock.Data = Array.Empty<byte>();
            }

            codeBlocks[i] = codeBlock;
        }

        return codeBlocks;
    }

    /// <summary>
    /// Validates that the header contains required information for packet parsing.
    /// </summary>
    private void ValidateHeader()
    {
        if (_header.CodingStyle == null)
        {
            throw new InvalidOperationException("JPX CodingStyle is required to parse packets.");
        }

        int layers = _header.CodingStyle.NumberOfLayers;
        int resolutions = _header.CodingStyle.DecompositionLevels;
        int components = _header.ComponentCount;
        
        if (layers <= 0 || resolutions < 0 || components <= 0)
        {
            throw new InvalidOperationException("Invalid JPX header values for packet enumeration.");
        }
    }
}

/// <summary>
/// Represents coordinates for a single packet in the progression order.
/// </summary>
internal readonly struct PacketCoordinate
{
    public readonly int Layer;
    public readonly int Resolution;
    public readonly int Component;
    public readonly int PrecinctX;
    public readonly int PrecinctY;

    public PacketCoordinate(int layer, int resolution, int component, int precinctX, int precinctY)
    {
        Layer = layer;
        Resolution = resolution;
        Component = component;
        PrecinctX = precinctX;
        PrecinctY = precinctY;
    }
}

/// <summary>
/// Common utilities for packet enumeration across different progression orders.
/// </summary>
internal static class JpxPacketEnumerationHelper
{
    /// <summary>
    /// Calculates the width of a tile based on SIZ marker information.
    /// </summary>
    public static int CalculateTileWidth(JpxHeader header, JpxTileHeader tileHeader)
    {
        // Use proper SIZ marker information from header
        if (header.TileWidth > 0)
        {
            // Calculate actual tile width considering tile boundaries
            uint tileIndex = tileHeader.TileIndex;
            int tilesX = tileHeader.TilesHorizontal;
            
            if (tilesX <= 0) tilesX = 1;
            
            int tileX = (int)(tileIndex % tilesX);
            uint tileStartX = header.TileOriginX + (uint)(tileX * header.TileWidth);
            uint tileEndX = Math.Min(tileStartX + header.TileWidth, header.Width);
            
            return (int)(tileEndX - tileStartX);
        }
        
        // Fallback: assume single tile spans full image width
        return (int)header.Width / Math.Max(tileHeader.TilesHorizontal, 1);
    }

    /// <summary>
    /// Calculates the height of a tile based on SIZ marker information.
    /// </summary>
    public static int CalculateTileHeight(JpxHeader header, JpxTileHeader tileHeader)
    {
        // Use proper SIZ marker information from header
        if (header.TileHeight > 0)
        {
            // Calculate actual tile height considering tile boundaries
            uint tileIndex = tileHeader.TileIndex;
            int tilesX = tileHeader.TilesHorizontal;
            int tilesY = tileHeader.TilesVertical;
            
            if (tilesX <= 0) tilesX = 1;
            if (tilesY <= 0) tilesY = 1;
            
            int tileY = (int)(tileIndex / tilesX);
            uint tileStartY = header.TileOriginY + (uint)(tileY * header.TileHeight);
            uint tileEndY = Math.Min(tileStartY + header.TileHeight, header.Height);
            
            return (int)(tileEndY - tileStartY);
        }
        
        // Fallback: assume single tile spans full image height
        return (int)header.Height / Math.Max(tileHeader.TilesVertical, 1);
    }

    /// <summary>
    /// Enumerates packet coordinates in LRCP (Layer-Resolution-Component-Position) order.
    /// </summary>
    public static IEnumerable<PacketCoordinate> EnumerateLrcp(JpxHeader header, JpxTileHeader tileHeader)
    {
        int layers = header.CodingStyle.NumberOfLayers;
        int resolutions = header.CodingStyle.DecompositionLevels;
        int components = header.ComponentCount;

        int tileWidth = CalculateTileWidth(header, tileHeader);
        int tileHeight = CalculateTileHeight(header, tileHeader);

        for (int layer = 0; layer < layers; layer++)
        {
            for (int resolution = 0; resolution <= resolutions; resolution++)
            {
                (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                    tileWidth, tileHeight, resolution, header.CodingStyle);

                for (int component = 0; component < components; component++)
                {
                    for (int py = 0; py < precinctsY; py++)
                    {
                        for (int px = 0; px < precinctsX; px++)
                        {
                            yield return new PacketCoordinate(layer, resolution, component, px, py);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enumerates packet coordinates in RLCP (Resolution-Layer-Component-Position) order.
    /// </summary>
    public static IEnumerable<PacketCoordinate> EnumerateRlcp(JpxHeader header, JpxTileHeader tileHeader)
    {
        int layers = header.CodingStyle.NumberOfLayers;
        int resolutions = header.CodingStyle.DecompositionLevels;
        int components = header.ComponentCount;

        int tileWidth = CalculateTileWidth(header, tileHeader);
        int tileHeight = CalculateTileHeight(header, tileHeader);

        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, header.CodingStyle);

            for (int layer = 0; layer < layers; layer++)
            {
                for (int component = 0; component < components; component++)
                {
                    for (int py = 0; py < precinctsY; py++)
                    {
                        for (int px = 0; px < precinctsX; px++)
                        {
                            yield return new PacketCoordinate(layer, resolution, component, px, py);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enumerates packet coordinates in RPCL (Resolution-Position-Component-Layer) order.
    /// </summary>
    public static IEnumerable<PacketCoordinate> EnumerateRpcl(JpxHeader header, JpxTileHeader tileHeader)
    {
        int layers = header.CodingStyle.NumberOfLayers;
        int resolutions = header.CodingStyle.DecompositionLevels;
        int components = header.ComponentCount;

        int tileWidth = CalculateTileWidth(header, tileHeader);
        int tileHeight = CalculateTileHeight(header, tileHeader);

        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, header.CodingStyle);

            for (int py = 0; py < precinctsY; py++)
            {
                for (int px = 0; px < precinctsX; px++)
                {
                    for (int component = 0; component < components; component++)
                    {
                        for (int layer = 0; layer < layers; layer++)
                        {
                            yield return new PacketCoordinate(layer, resolution, component, px, py);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enumerates packet coordinates in PCRL (Position-Component-Resolution-Layer) order.
    /// </summary>
    public static IEnumerable<PacketCoordinate> EnumeratePcrl(JpxHeader header, JpxTileHeader tileHeader)
    {
        int layers = header.CodingStyle.NumberOfLayers;
        int resolutions = header.CodingStyle.DecompositionLevels;
        int components = header.ComponentCount;

        // Calculate max precinct grid across all resolutions
        int tileWidth = CalculateTileWidth(header, tileHeader);
        int tileHeight = CalculateTileHeight(header, tileHeader);
        
        int maxPrecinctsX = 0;
        int maxPrecinctsY = 0;
        
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, header.CodingStyle);
            maxPrecinctsX = Math.Max(maxPrecinctsX, precinctsX);
            maxPrecinctsY = Math.Max(maxPrecinctsY, precinctsY);
        }

        for (int py = 0; py < maxPrecinctsY; py++)
        {
            for (int px = 0; px < maxPrecinctsX; px++)
            {
                for (int component = 0; component < components; component++)
                {
                    for (int resolution = 0; resolution <= resolutions; resolution++)
                    {
                        (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                            tileWidth, tileHeight, resolution, header.CodingStyle);
                        
                        // Only emit if precinct exists at this resolution
                        if (px < precinctsX && py < precinctsY)
                        {
                            for (int layer = 0; layer < layers; layer++)
                            {
                                yield return new PacketCoordinate(layer, resolution, component, px, py);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enumerates packet coordinates in CPRL (Component-Position-Resolution-Layer) order.
    /// </summary>
    public static IEnumerable<PacketCoordinate> EnumerateCprl(JpxHeader header, JpxTileHeader tileHeader)
    {
        int layers = header.CodingStyle.NumberOfLayers;
        int resolutions = header.CodingStyle.DecompositionLevels;
        int components = header.ComponentCount;

        // Calculate max precinct grid across all resolutions
        int tileWidth = CalculateTileWidth(header, tileHeader);
        int tileHeight = CalculateTileHeight(header, tileHeader);
        
        int maxPrecinctsX = 0;
        int maxPrecinctsY = 0;
        
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, header.CodingStyle);
            maxPrecinctsX = Math.Max(maxPrecinctsX, precinctsX);
            maxPrecinctsY = Math.Max(maxPrecinctsY, precinctsY);
        }

        for (int component = 0; component < components; component++)
        {
            for (int py = 0; py < maxPrecinctsY; py++)
            {
                for (int px = 0; px < maxPrecinctsX; px++)
                {
                    for (int resolution = 0; resolution <= resolutions; resolution++)
                    {
                        (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                            tileWidth, tileHeight, resolution, header.CodingStyle);
                        
                        // Only emit if precinct exists at this resolution
                        if (px < precinctsX && py < precinctsY)
                        {
                            for (int layer = 0; layer < layers; layer++)
                            {
                                yield return new PacketCoordinate(layer, resolution, component, px, py);
                            }
                        }
                    }
                }
            }
        }
    }
}