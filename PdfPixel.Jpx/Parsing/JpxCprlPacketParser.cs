using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Component-Position-Resolution-Layer progression order.
/// Outer loop: components, then precincts (max grid), then resolutions, then layers.
/// </summary>
internal sealed class JpxCprlPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxCprlPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.CPRL;

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

        // Calculate max precinct grid and count total packets
        int maxPrecinctsX = 0;
        int maxPrecinctsY = 0;
        int totalPackets = 0;

        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileWidth, tileHeight, resolution, _header.CodingStyle);
            maxPrecinctsX = Math.Max(maxPrecinctsX, precinctsX);
            maxPrecinctsY = Math.Max(maxPrecinctsY, precinctsY);
            totalPackets += layers * components * precinctsX * precinctsY;
        }

        if (totalPackets == 0)
        {
            return Array.Empty<JpxPacket>();
        }

        var packets = new JpxPacket[totalPackets];
        int index = 0;

        // Fill coordinates in CPRL order: components, precincts, resolutions, layers
        for (int component = 0; component < components; component++)
        {
            for (int py = 0; py < maxPrecinctsY; py++)
            {
                for (int px = 0; px < maxPrecinctsX; px++)
                {
                    for (int resolution = 0; resolution <= resolutions; resolution++)
                    {
                        (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                            tileWidth, tileHeight, resolution, _header.CodingStyle);

                        // Only emit if precinct exists at this resolution
                        if (px < precinctsX && py < precinctsY)
                        {
                            for (int layer = 0; layer < layers; layer++)
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
        }

        JpxPacketCodeBlockParser codeBlockParser = new(_header, tileHeader);
        codeBlockParser.ParseCodeBlocks(packetData, packets);

        return packets;
    }
}
