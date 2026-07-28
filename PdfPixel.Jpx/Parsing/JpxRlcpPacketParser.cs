using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Resolution-Layer-Component-Position progression order.
/// Outer loop: resolutions, then layers, then components, then precincts.
/// </summary>
internal sealed class JpxRlcpPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxRlcpPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.RLCP;

    /// <inheritdoc />
    public IReadOnlyList<JpxCodeBlock> ParseCodeBlocks(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        if (_header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        int layers = _header.CodingStyle.NumberOfLayers;
        int resolutions = _header.CodingStyle.DecompositionLevels;
        int components = _header.ComponentCount;

        JpxRectangle tileBounds = JpxPacketEnumerationHelper.CalculateTileBounds(_header, tileHeader);

        // Count total packets
        int totalPackets = 0;
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileBounds, resolution, _header.CodingStyle);
            totalPackets += layers * components * precinctsX * precinctsY;
        }

        if (totalPackets == 0)
        {
            return Array.Empty<JpxCodeBlock>();
        }

        var packets = new JpxPacket[totalPackets];
        int index = 0;

        // Fill coordinates in RLCP order: resolutions, layers, components, precincts
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileBounds, resolution, _header.CodingStyle);

            for (int layer = 0; layer < layers; layer++)
            {
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

        return codeBlockParser.ParseCodeBlocks(packetData, packets);
    }
}
