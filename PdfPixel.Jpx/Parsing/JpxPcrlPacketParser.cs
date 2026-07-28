using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Position-Component-Resolution-Layer progression order.
/// Outer loop: reference grid position, then components, then resolutions, then layers.
/// </summary>
internal sealed class JpxPcrlPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxPcrlPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.PCRL;

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

        int totalPackets = 0;

        for (int resolution = 0; resolution <= resolutions; resolution++)
        {
            (int precinctsX, int precinctsY) = JpxPrecinctHelper.ComputePrecinctGrid(
                tileBounds, resolution, _header.CodingStyle);
            totalPackets += layers * components * precinctsX * precinctsY;
        }

        (int stepX, int stepY) = JpxPositionProgression.ComputeStep(_header, 0, components - 1);

        if (totalPackets == 0 || stepX == 0 || stepY == 0)
        {
            return Array.Empty<JpxCodeBlock>();
        }

        var packets = new JpxPacket[totalPackets];
        int index = 0;

        // Fill coordinates in PCRL order: positions, components, resolutions, layers
        for (int y = tileBounds.Y; y < tileBounds.Bottom; y += stepY - (y % stepY))
        {
            for (int x = tileBounds.X; x < tileBounds.Right; x += stepX - (x % stepX))
            {
                for (int component = 0; component < components; component++)
                {
                    for (int resolution = 0; resolution <= resolutions; resolution++)
                    {
                        if (!JpxPositionProgression.TryGetPrecinctAt(
                            _header, tileBounds, component, resolution, x, y, out int precinctX, out int precinctY))
                        {
                            continue;
                        }

                        for (int layer = 0; layer < layers; layer++)
                        {
                            packets[index++] = new JpxPacket
                            {
                                Layer = layer,
                                Resolution = resolution,
                                Component = component,
                                PrecinctX = precinctX,
                                PrecinctY = precinctY
                            };
                        }
                    }
                }
            }
        }

        if (index != packets.Length)
        {
            Array.Resize(ref packets, index);
        }

        JpxPacketCodeBlockParser codeBlockParser = new(_header, tileHeader);

        return codeBlockParser.ParseCodeBlocks(packetData, packets);
    }
}
