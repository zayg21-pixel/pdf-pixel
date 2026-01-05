using PdfReader.Imaging.Jpx.Model;
using System;

namespace PdfReader.Imaging.Jpx.Parsing;

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

/// <summary>
/// Packet parser for Layer-Resolution-Component-Position progression order.
/// Most common progression order for simple images.
/// Thin wrapper around common parsing engine with LRCP enumeration order.
/// </summary>
internal sealed class JpxLrcpPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxLrcpPacketParser(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.LRCP;

    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        var engine = new JpxPacketParsingEngine(_header, tileHeader);
        var enumerationOrder = JpxPacketEnumerationHelper.EnumerateLrcp(_header, tileHeader);
        return engine.ParsePackets(packetData, enumerationOrder);
    }
}

/// <summary>
/// Packet parser for Resolution-Layer-Component-Position progression order.
/// Alternative progression order, less common.
/// Thin wrapper around common parsing engine with RLCP enumeration order.
/// </summary>
internal sealed class JpxRlcpPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxRlcpPacketParser(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.RLCP;

    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        var engine = new JpxPacketParsingEngine(_header, tileHeader);
        var enumerationOrder = JpxPacketEnumerationHelper.EnumerateRlcp(_header, tileHeader);
        return engine.ParsePackets(packetData, enumerationOrder);
    }
}

/// <summary>
/// Packet parser for Resolution-Position-Component-Layer progression order.
/// Thin wrapper around common parsing engine with RPCL enumeration order.
/// </summary>
internal sealed class JpxRpclPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxRpclPacketParser(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.RPCL;

    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        var engine = new JpxPacketParsingEngine(_header, tileHeader);
        var enumerationOrder = JpxPacketEnumerationHelper.EnumerateRpcl(_header, tileHeader);
        return engine.ParsePackets(packetData, enumerationOrder);
    }
}

/// <summary>
/// Packet parser for Position-Component-Resolution-Layer progression order.
/// Thin wrapper around common parsing engine with PCRL enumeration order.
/// </summary>
internal sealed class JpxPcrlPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxPcrlPacketParser(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.PCRL;

    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        var engine = new JpxPacketParsingEngine(_header, tileHeader);
        var enumerationOrder = JpxPacketEnumerationHelper.EnumeratePcrl(_header, tileHeader);
        return engine.ParsePackets(packetData, enumerationOrder);
    }
}

/// <summary>
/// Packet parser for Component-Position-Resolution-Layer progression order.
/// Thin wrapper around common parsing engine with CPRL enumeration order.
/// </summary>
internal sealed class JpxCprlPacketParser : IJpxPacketParser
{
    private readonly JpxHeader _header;

    public JpxCprlPacketParser(JpxHeader header)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
    }

    public JpxProgressionOrder ProgressionOrder => JpxProgressionOrder.CPRL;

    public JpxPacket[] ParsePackets(ReadOnlySpan<byte> packetData, JpxTileHeader tileHeader)
    {
        var engine = new JpxPacketParsingEngine(_header, tileHeader);
        var enumerationOrder = JpxPacketEnumerationHelper.EnumerateCprl(_header, tileHeader);
        return engine.ParsePackets(packetData, enumerationOrder);
    }
}