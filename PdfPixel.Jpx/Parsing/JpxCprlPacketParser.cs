using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet parser for Component-Position-Resolution-Layer progression order.
/// Outer loop: components, then reference grid position, then resolutions, then layers.
/// </summary>
internal sealed class JpxCprlPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxCprlPacketParser(JpxHeader header) => _header = header ?? throw new ArgumentNullException(nameof(header));

    /// <inheritdoc />
    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.CPRL;

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

        if (totalPackets == 0)
        {
            return Array.Empty<JpxCodeBlock>();
        }

        var packets = new JpxPacket[totalPackets];
        int index = 0;

        // Fill coordinates in CPRL order: components, positions, resolutions, layers.
        // The position step is per component, since it follows that component's subsampling.
        for (int component = 0; component < components; component++)
        {
            (int stepX, int stepY) = JpxPositionProgression.ComputeStep(_header, component, component);

            if (stepX == 0 || stepY == 0)
            {
                continue;
            }

            for (int y = tileBounds.Y; y < tileBounds.Bottom; y += stepY - (y % stepY))
            {
                for (int x = tileBounds.X; x < tileBounds.Right; x += stepX - (x % stepX))
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
