using PdfRender.Imaging.Jpx.Model;
using System;

namespace PdfRender.Imaging.Jpx.Parsing;

/// <summary>
/// Interface for parsing JPEG2000 packets from tile data according to progression order.
/// </summary>
internal interface IJpxPacketParser
{
    /// <summary>
    /// Parses packets from tile data according to the specific progression order implementation.
    /// </summary>
    /// <param name="packetData">Raw packet data from tile.</param>
    /// <param name="tileHeader">Tile-specific header information.</param>
    /// <returns>Array of parsed packets.</returns>
    JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader);
    
    /// <summary>
    /// Gets the progression order that this parser handles.
    /// </summary>
    JpxProgressionOrder ProgressionOrder { get; }
}