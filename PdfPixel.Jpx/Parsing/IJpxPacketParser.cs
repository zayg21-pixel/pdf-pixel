using PdfPixel.Jpx.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Interface for parsing JPEG2000 packets from tile data according to progression order.
/// </summary>
internal interface IJpxPacketParser
{
    /// <summary>
    /// Parses the tile's packets in this parser's progression order, accumulating each
    /// quality layer's entropy-coded bytes into the code-block it belongs to.
    /// </summary>
    /// <param name="packetData">Raw packet data from tile.</param>
    /// <param name="tileHeader">Tile-specific header information.</param>
    /// <returns>
    /// Every distinct code-block in the tile, each carrying the data accumulated across all its layers.
    /// </returns>
    IReadOnlyList<JpxCodeBlock> ParseCodeBlocks(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader);

    /// <summary>
    /// Gets the progression order that this parser handles.
    /// </summary>
    JpxProgressionOrder ProgressionOrder { get; }
}
