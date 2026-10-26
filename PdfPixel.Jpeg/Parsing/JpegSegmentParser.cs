using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Jpeg.Parsing;

/// <summary>
/// Parses JPEG marker segment payloads into <see cref="JpegHeader"/> data.
/// </summary>
internal static class JpegSegmentParser
{
    // JPEG markers
    private const byte MarkerPrefix = 0xFF;
    private const byte EOI = 0xD9;
    private const byte SOS = 0xDA;
    private const byte DQT = 0xDB;
    private const byte DHT = 0xC4;
    private const byte DRI = 0xDD;
    private const byte RST0 = 0xD0;
    private const byte RST7 = 0xD7;
    private const byte APP0 = 0xE0;
    private const byte APP1 = 0xE1;
    private const byte APP2 = 0xE2;
    private const byte APP14 = 0xEE;

    private static readonly HashSet<byte> SofMarkers = [
        0xC0,
        0xC1,
        0xC2,
        0xC3,
        0xC5,
        0xC6,
        0xC7,
        0xC9,
        0xCA,
        0xCB,
        0xCD,
        0xCE,
        0xCF
    ];

    /// <summary>
    /// Parses one marker segment other than SOI, EOI and SOS into <paramref name="header"/>.
    /// Segments of other markers are skipped.
    /// </summary>
    /// <param name="marker">Marker byte following the 0xFF prefix.</param>
    /// <param name="payload">Segment payload bytes (excluding the 2-byte length field).</param>
    /// <param name="header">Header receiving the parsed data.</param>
    public static void ParseSegment(byte marker, in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        if (marker == DQT)
        {
            header.HasQuantizationTables = true;
            List<JpegQuantizationTable> qTables = ParseDqtPayload(payload);
            header.QuantizationTables.AddRange(qTables);
        }
        else if (marker == DHT)
        {
            header.HasHuffmanTables = true;
            List<JpegHuffmanTable> hTables = ParseDhtPayload(payload);
            header.HuffmanTables.AddRange(hTables);
        }
        else if (marker == DRI)
        {
            if (payload.Length >= 2)
            {
                header.RestartInterval = ReadUInt16BE(payload);
            }
        }
        else if (SofMarkers.Contains(marker))
        {
            ParseSof(marker, payload, header);
        }
        else if (marker == APP0)
        {
            ParseApp0(payload, header);
        }
        else if (marker == APP1)
        {
            ParseApp1(payload, header);
        }
        else if (marker == APP2)
        {
            ParseApp2Icc(payload, header);
        }
        else if (marker == APP14)
        {
            ParseApp14Adobe(payload, header);
        }
    }

    /// <summary>
    /// Reports whether a further scan follows the entropy-coded data starting at <paramref name="start"/>,
    /// by walking forward to the next marker that is not part of that data.
    /// </summary>
    /// <param name="bytes">Complete JPEG byte span.</param>
    /// <param name="start">Offset of the entropy-coded data following the first SOS header.</param>
    public static bool HasFurtherScan(in ReadOnlySpan<byte> bytes, int start)
    {
        int position = start;

        while (position + 1 < bytes.Length)
        {
            if (bytes[position++] != MarkerPrefix)
            {
                continue;
            }

            while (position < bytes.Length && bytes[position] == MarkerPrefix)
            {
                position++;
            }

            if (position >= bytes.Length)
            {
                return false;
            }

            byte code = bytes[position++];

            // A stuffed zero and a restart marker both belong to the entropy-coded data.
            if (code == 0x00 || (code >= RST0 && code <= RST7))
            {
                continue;
            }

            if (code == SOS)
            {
                return true;
            }

            if (code == EOI)
            {
                return false;
            }

            if (position + 2 > bytes.Length)
            {
                return false;
            }

            var segmentLength = (ushort)((bytes[position] << 8) | bytes[position + 1]);
            int payloadLength = segmentLength - 2;
            position += 2;

            if (payloadLength < 0 || position + payloadLength > bytes.Length)
            {
                return false;
            }

            position += payloadLength;
        }

        return false;
    }

    /// <summary>
    /// Parses an SOS (Start of Scan) segment payload into a <see cref="JpegScanSpec"/>.
    /// </summary>
    /// <param name="payload">Segment payload bytes (excluding the 2-byte length field).</param>
    /// <returns>Populated <see cref="JpegScanSpec"/> with component selectors and spectral/successive-approximation parameters.</returns>
    public static JpegScanSpec ParseSos(in ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1)
        {
            throw new InvalidDataException("Invalid SOS: too short");
        }

        int ns = payload[0];
        int offset = 1;
        JpegScanSpec scan = new();
        for (int i = 0; i < ns; i++)
        {
            if (offset + 2 >= payload.Length)
            {
                throw new InvalidDataException("Invalid SOS: component selector truncated");
            }

            byte cs = payload[offset + 0];
            byte tdTa = payload[offset + 1];
            offset += 2;

            scan.Components.Add(new JpegScanComponentSpec
            {
                ComponentId = cs,
                DcTableId = tdTa >> 4 & 0x0F,
                AcTableId = tdTa & 0x0F
            });
        }

        if (offset + 3 > payload.Length)
        {
            throw new InvalidDataException("Invalid SOS: spectral/successive params missing");
        }

        scan.SpectralStart = payload[offset + 0];
        scan.SpectralEnd = payload[offset + 1];
        int ahAl = payload[offset + 2];
        scan.SuccessiveApproxHigh = ahAl >> 4 & 0x0F;
        scan.SuccessiveApproxLow = ahAl & 0x0F;

        return scan;
    }

    /// <summary>
    /// Parses one or more JPEG quantization tables from a DQT segment payload.
    /// </summary>
    /// <param name="payload">The raw DQT segment bytes.</param>
    /// <returns>A list of parsed <see cref="JpegQuantizationTable"/> instances.</returns>
    /// <exception cref="ArgumentException">Thrown if the payload is truncated or invalid.</exception>
    public static List<JpegQuantizationTable> ParseDqtPayload(in ReadOnlySpan<byte> payload)
    {
        List<JpegQuantizationTable> list = [];
        int offset = 0;
        while (offset < payload.Length)
        {
            if (offset + 1 >= payload.Length)
            {
                throw new ArgumentException("Invalid DQT segment: truncated specifier");
            }

            byte pqTq = payload[offset++];
            int precision = pqTq >> 4 & 0x0F; // 0 or 1
            int tableId = pqTq & 0x0F; // 0..3
            bool is16 = precision != 0;

            JpegQuantizationTable table = new()
            {
                TableId = tableId,
                Is16Bit = is16
            };

            int step = is16 ? 2 : 1;
            int required = 64 * step;
            if (offset + required > payload.Length)
            {
                throw new ArgumentException("Invalid DQT segment: table entries truncated");
            }

            if (!is16)
            {
                for (int i = 0; i < 64; i++)
                {
                    table.Entries[i] = payload[offset + i];
                }

                offset += 64;
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    table.Entries[i] = (ushort)(payload[offset + (2 * i)] << 8 | payload[offset + (2 * i) + 1]);
                }

                offset += 128;
            }

            list.Add(table);
        }

        return list;
    }

    /// <summary>
    /// Parse one or more Huffman tables from a DHT segment payload.
    /// </summary>
    public static List<JpegHuffmanTable> ParseDhtPayload(in ReadOnlySpan<byte> payload)
    {
        List<JpegHuffmanTable> list = [];
        int offset = 0;
        while (offset < payload.Length)
        {
            if (offset + 17 > payload.Length)
            {
                throw new ArgumentException("Invalid DHT segment: truncated header");
            }

            byte tcTh = payload[offset];
            offset++;

            int tableClass = tcTh >> 4 & 0x0F;
            int tableId = tcTh & 0x0F;

            var counts = new byte[JpegHuffmanTable.MaxCodeLength];
            int valueCount = 0;
            for (int i = 0; i < JpegHuffmanTable.MaxCodeLength; i++)
            {
                byte c = payload[offset + i];
                counts[i] = c;
                valueCount += c;
            }

            offset += JpegHuffmanTable.MaxCodeLength;
            if (offset + valueCount > payload.Length)
            {
                throw new ArgumentException("Invalid DHT segment: values truncated");
            }

            byte[] values = payload.Slice(offset, valueCount).ToArray();
            offset += valueCount;

            list.Add(new JpegHuffmanTable
                {
                    TableClass = tableClass,
                    TableId = tableId,
                    Values = values
                    // copy counts into instance array
                }
                .WithCounts(counts));
        }

        return list;
    }

    /// <summary>
    /// Reassembles ICC profile bytes from APP2 ICC_PROFILE segments.
    /// Returns null if segments are missing, inconsistent, invalid, or if length overflows.
    /// </summary>
    /// <param name="segments">ICC profile segments in the order they appear in the file.</param>
    public static byte[]? AssembleIccProfile(List<JpegIccSegmentInfo> segments)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        // Determine declared total segment count from first segment.
        int declaredTotal = segments[0].TotalSegments;
        if (declaredTotal <= 0 || declaredTotal > 255)
        {
            return null;
        }

        // Prepare storage for segment payloads in 1-based order (JPEG ICC spec sequences start at 1).
        var segmentPayloads = new byte[declaredTotal][];
        int totalLength = 0;

        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            JpegIccSegmentInfo segment = segments[segmentIndex];
            if (segment == null)
            {
                return null;
            }

            if (segment.TotalSegments != declaredTotal)
            {
                // Inconsistent total count across segments.
                return null;
            }

            int sequenceNumber = segment.SequenceNumber; // 1-based
            if (sequenceNumber <= 0 || sequenceNumber > declaredTotal)
            {
                return null;
            }

            if (segmentPayloads[sequenceNumber - 1] != null)
            {
                // Duplicate sequence number.
                return null;
            }

            byte[] data = segment.Data;
            if (data == null || data.Length == 0)
            {
                return null;
            }

            // Check for integer overflow before accumulation.
            if (data.Length > int.MaxValue - totalLength)
            {
                return null;
            }

            segmentPayloads[sequenceNumber - 1] = data;
            totalLength += data.Length;
        }

        // Verify that all segments were present.
        for (int i = 0; i < declaredTotal; i++)
        {
            if (segmentPayloads[i] == null)
            {
                return null;
            }
        }

        // Reassemble contiguously.
        var assembled = new byte[totalLength];
        int writeOffset = 0;
        for (int i = 0; i < segmentPayloads.Length; i++)
        {
            byte[] part = segmentPayloads[i];
            Buffer.BlockCopy(part, 0, assembled, writeOffset, part.Length);
            writeOffset += part.Length;
        }

        return assembled;
    }

    /// <summary>
    /// Reads a big-endian 16-bit value from the start of <paramref name="span"/>.
    /// </summary>
    public static ushort ReadUInt16BE(in ReadOnlySpan<byte> span) => (ushort)(span[0] << 8 | span[1]);

    private static void ParseSof(byte marker, in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        if (payload.Length < 6)
        {
            throw new InvalidDataException("Invalid SOF: too short");
        }

        header.FrameType = (JpegFrameType)marker;
        header.SamplePrecision = payload[0];
        header.Height = ReadUInt16BE(payload.Slice(1));
        header.Width = ReadUInt16BE(payload.Slice(3));
        int components = payload[5];
        header.ComponentCount = components;

        int offset = 6;
        header.Components.Clear();

        for (int i = 0; i < components; i++)
        {
            if (offset + 3 > payload.Length)
            {
                throw new InvalidDataException("Invalid SOF: component table truncated");
            }

            JpegComponent comp = new()
            {
                Id = payload[offset + 0],
                HorizontalSamplingFactor = (byte)(payload[offset + 1] >> 4),
                VerticalSamplingFactor = (byte)(payload[offset + 1] & 0x0F),
                QuantizationTableId = payload[offset + 2]
            };
            header.Components.Add(comp);
            offset += 3;
        }
    }

    private static void ParseApp0(in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        if (payload.Length >= 5 && payload[0] == (byte)'J' && payload[1] == (byte)'F' && payload[2] == (byte)'I' && payload[3] == (byte)'F' && payload[4] == 0)
        {
            header.IsJfif = true;
            if (payload.Length >= 14)
            {
                header.JfifVersion = (ushort)(payload[5] << 8 | payload[6]);
                header.DensityUnits = payload[7];
                header.XDensity = ReadUInt16BE(payload.Slice(8));
                header.YDensity = ReadUInt16BE(payload.Slice(10));
            }
        }
    }

    private static void ParseApp1(in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        if (payload.Length >= 6 && payload[0] == (byte)'E' && payload[1] == (byte)'x' && payload[2] == (byte)'i' && payload[3] == (byte)'f' && payload[4] == 0 && payload[5] == 0)
        {
            header.IsExif = true;
        }
    }

    private static void ParseApp2Icc(in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        const string ICC = "ICC_PROFILE\0";
        if (payload.Length >= 14)
        {
            var match = true;
            for (int i = 0; i < ICC.Length; i++)
            {
                if (payload[i] != (byte)ICC[i])
                {
                    match = false;
                    break;
                }
            }

            if (match && payload.Length >= 16)
            {
                header.HasIccProfile = true;
                int seq = payload[12];
                int total = payload[13];
                byte[] data = payload.Slice(14).ToArray();
                header.IccProfileSegments.Add(new JpegIccSegmentInfo
                {
                    SequenceNumber = seq,
                    TotalSegments = total,
                    Data = data
                });
            }
        }
    }

    private static void ParseApp14Adobe(in ReadOnlySpan<byte> payload, JpegHeader header)
    {
        if (payload.Length >= 12
            && payload[0] == (byte)'A'
            && payload[1] == (byte)'d'
            && payload[2] == (byte)'o'
            && payload[3] == (byte)'b'
            && payload[4] == (byte)'e')
        {
            header.HasAdobeApp14 = true;
            header.AdobeColorTransform = payload[11];
        }
    }
}
