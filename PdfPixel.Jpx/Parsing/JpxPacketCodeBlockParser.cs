using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Parses code-block data for pre-allocated JPEG 2000 (JPX) packets.
/// Packets must already have their coordinates (Layer, Resolution, Component, PrecinctX, PrecinctY) set.
/// This class enriches each packet with parsed code-block information.
/// </summary>
internal sealed class JpxPacketCodeBlockParser
{
    private readonly JpxHeader _header;
    private readonly JpxTileHeader _tileHeader;
    private readonly JpxPacketHeaderParser _headerParser;

    public JpxPacketCodeBlockParser(JpxHeader header, JpxTileHeader tileHeader)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _tileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        _headerParser = new JpxPacketHeaderParser(header, tileHeader);
    }

    /// <summary>
    /// Parses every packet in <paramref name="packets"/>, accumulating each layer's
    /// entropy-coded bytes into the persistent code-blocks they belong to.
    /// </summary>
    /// <param name="packetData">Raw packet data to parse.</param>
    /// <param name="packets">Packet coordinates in codestream order.</param>
    /// <returns>
    /// Every distinct code-block in the tile, each carrying the data accumulated across all its layers.
    /// </returns>
    public IReadOnlyList<JpxCodeBlock> ParseCodeBlocks(in ReadOnlySpan<byte> packetData, JpxPacket[] packets)
    {
        if (packetData.Length == 0 || packets.Length == 0)
        {
            return Array.Empty<JpxCodeBlock>();
        }

        ValidateHeader();

        JpxBitReader bitReader = new(packetData);

        for (int index = 0; index < packets.Length; index++)
        {
            ParseSinglePacket(ref bitReader, packets[index]);
        }

        return _headerParser.CodeBlocks;
    }

    /// <summary>
    /// Parses a single packet, appending its layer contribution to the included code-blocks.
    /// </summary>
    private void ParseSinglePacket(ref JpxBitReader bitReader, JpxPacket packet)
    {
        ReadOnlySpan<JpxCodeBlock> includedBlocks = _headerParser.ParsePacketHeader(
            ref bitReader,
            packet.Layer,
            packet.Resolution,
            packet.Component,
            packet.PrecinctX,
            packet.PrecinctY);

        ParsePacketBody(ref bitReader, includedBlocks);
    }

    /// <summary>
    /// Parses the packet body to read and append raw code-block data.
    /// Per ITU-T T.800 B.10.7, code-block data immediately follows the packet header
    /// in the order code-blocks appeared in the header; the header parser leaves the
    /// reader byte-aligned at the start of the body.
    /// Data is appended to the persistent code-block objects via <see cref="JpxCodeBlock.AppendLayer"/>.
    /// </summary>
    private static void ParsePacketBody(ref JpxBitReader bitReader, in ReadOnlySpan<JpxCodeBlock> includedBlocks)
    {
        for (int i = 0; i < includedBlocks.Length; i++)
        {
            JpxCodeBlock block = includedBlocks[i];
            int dataLength = block.DataLength;

            if (dataLength > 0 && bitReader.Remaining >= dataLength)
            {
                ReadOnlySpan<byte> layerSpan = bitReader.ReadRawSpan(dataLength);
                block.AppendLayer(layerSpan, block.LayerCodingPasses);
            }
        }
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
