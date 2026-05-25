using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Layer-Resolution-Component-Position progression order.
/// Most common progression order for simple images.
/// </summary>
internal sealed class JpxLrcpPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxLrcpPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.LRCP;

    /// <inheritdoc />
    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        if (_header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        int layers = _header.CodingStyle.NumberOfLayers;
        int resolutions = _header.CodingStyle.DecompositionLevels;
        int components = _header.ComponentCount;

        int tileWidth = JpxPacketEnumerationHelper.CalculateTileWidth(_header, tileHeader);
        int tileHeight = JpxPacketEnumerationHelper.CalculateTileHeight(_header, tileHeader);

        // Count total packets
        int totalPackets = 0;
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, _header.CodingStyle);
            totalPackets += layers * components * precinctsX * precinctsY;
        }

        if (totalPackets == 0)
        {
            return Array.Empty<JpxPacket>();
        }

        var packets = new JpxPacket[totalPackets];
        int index = 0;

        // Fill coordinates in LRCP order: layers, resolutions, components, precincts
        for (int layer = 0; layer < layers; layer++)
        {
            for (int resolution = 0; resolution <= resolutions; resolution++)
            {
                (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                    tileWidth, tileHeight, resolution, _header.CodingStyle);

                for (int component = 0; component < components; component++)
                {
                    for (int py = 0; py < precinctsY; py++)
                    {
                        for (int px = 0; px < precinctsX; px++)
                        {
                            packets[index++] = new JpxPacket
                            {
                                Layer = layer,
                                Resolution = resolution,
                                Component = component,
                                PrecinctX = px,
                                PrecinctY = py
                            };
                        }
                    }
                }
            }
        }

        JpxPacketCodeBlockParser codeBlockParser = new(_header, tileHeader);
        codeBlockParser.ParseCodeBlocks(packetData, packets);

        return packets;
    }
}
