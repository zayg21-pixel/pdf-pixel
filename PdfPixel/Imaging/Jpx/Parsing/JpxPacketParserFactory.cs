using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Parsing;

/// <summary>
/// Factory for creating appropriate packet parsers based on progression order.
/// </summary>
internal static class JpxPacketParserFactory
{
    /// <summary>
    /// Creates a packet parser for the specified progression order with injected header.
    /// </summary>
    /// <param name="progressionOrder">JPEG2000 progression order.</param>
    /// <param name="header">JPX header containing coding parameters.</param>
    /// <returns>Packet parser implementation for the progression order.</returns>
    public static IJpxPacketParser CreateParser(JpxProgressionOrder progressionOrder, JpxHeader header)
    {
        return progressionOrder switch
        {
            JpxProgressionOrder.LRCP => new JpxLrcpPacketParser(header),
            JpxProgressionOrder.RLCP => new JpxRlcpPacketParser(header),
            JpxProgressionOrder.RPCL => new JpxRpclPacketParser(header),
            JpxProgressionOrder.PCRL => new JpxPcrlPacketParser(header),
            JpxProgressionOrder.CPRL => new JpxCprlPacketParser(header),
            _ => throw new ArgumentException($"Unknown progression order: {progressionOrder}")
        };
    }
}