using PdfPixel.Jpx.Model;
using System;

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
    /// Parses code-block data into the provided packet array.
    /// Each packet must already have coordinates set; this method populates CodeBlocks.
    /// </summary>
    /// <param name="packetData">Raw packet data to parse.</param>
    /// <param name="packets">Pre-allocated packets with coordinates initialized.</param>
    public void ParseCodeBlocks(ReadOnlySpan<byte> packetData, JpxPacket[] packets)
    {
        if (packetData.Length == 0 || packets.Length == 0)
        {
            return;
        }

        ValidateHeader();

        var bitReader = new JpxBitReader(packetData);

        for (int index = 0; index < packets.Length; index++)
        {
            ParseSinglePacket(ref bitReader, ref packets[index]);
        }
    }

    /// <summary>
    /// Parses a single packet's code-block data.
    /// </summary>
    private void ParseSinglePacket(ref JpxBitReader bitReader, ref JpxPacket packet)
    {
        // Parse packet header
        var headerInfo = _headerParser.ParsePacketHeader(
            ref bitReader,
            packet.Layer,
            packet.Resolution,
            packet.Component,
            packet.PrecinctX,
            packet.PrecinctY);

        if (!headerInfo.IsEmpty)
        {
            // Snapshot per-layer transient values before body parsing overwrites them in future layers
            var layerBlocks = headerInfo.CodeBlocks;
            int[] layerDataLengths = new int[layerBlocks.Length];
            int[] layerPasses = new int[layerBlocks.Length];

            for (int i = 0; i < layerBlocks.Length; i++)
            {
                layerDataLengths[i] = layerBlocks[i].DataLength;
                layerPasses[i] = layerBlocks[i].LayerCodingPasses;
            }

            // Parse packet body — appends this layer's data to persistent code-blocks
            ParsePacketBody(ref bitReader, layerBlocks);

            // Create independent snapshot code-blocks for this packet
            var codeBlocks = new JpxCodeBlock[layerBlocks.Length];
            for (int i = 0; i < layerBlocks.Length; i++)
            {
                codeBlocks[i] = layerBlocks[i].CreateLayerSnapshot(layerDataLengths[i], layerPasses[i]);
            }

            packet.CodeBlocks = codeBlocks;
        }
        else
        {
            packet.CodeBlocks = Array.Empty<JpxCodeBlock>();
        }
    }

    /// <summary>
    /// Parses the packet body to read and append raw code-block data.
    /// Per ITU-T T.800 B.10.7, code-block data immediately follows the packet header
    /// (after byte alignment) in the order code-blocks appeared in the header.
    /// Data is appended to the persistent code-block objects via <see cref="JpxCodeBlock.AppendLayer"/>.
    /// </summary>
    private static void ParsePacketBody(ref JpxBitReader bitReader, JpxCodeBlock[] headerCodeBlocks)
    {
        // Align to byte boundary before reading code-block data
        bitReader.ByteAlign();

        for (int i = 0; i < headerCodeBlocks.Length; i++)
        {
            var block = headerCodeBlocks[i];
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