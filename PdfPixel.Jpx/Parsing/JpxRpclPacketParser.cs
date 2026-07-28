using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Resolution-Position-Component-Layer progression order.
/// Outer loop: resolutions, then precincts, then components, then layers.
/// </summary>
internal sealed class JpxRpclPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxRpclPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.RPCL;

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

        // Fill coordinates in RPCL order: resolutions, precincts, components, layers
        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileBounds, resolution, _header.CodingStyle);

            for (int py = 0; py < precinctsY; py++)
            {
                for (int px = 0; px < precinctsX; px++)
                {
                    for (int component = 0; component < components; component++)
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

        JpxPacketCodeBlockParser codeBlockParser = new(_header, tileHeader);

        return codeBlockParser.ParseCodeBlocks(packetData, packets);
    }
}
