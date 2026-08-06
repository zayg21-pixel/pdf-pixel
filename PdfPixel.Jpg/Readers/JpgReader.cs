using PdfPixel.Jpg.Huffman;
using PdfPixel.Jpg.Model;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPixel.Jpg.Readers;

/// <summary>
/// Minimal JPEG header reader that parses marker segments up to the first SOS and collects
/// metadata needed by our custom decoder (quant tables presence, Huffman presence, components,
/// APP markers like JFIF/Adobe/ICC). It does not parse entropy-coded data.
/// Additionally, records the offset to the entropy-coded data in the returned header and parses DRI/SOS.
/// Also scans after the first SOS for any DQT/DHT/DRI/DNL segments that may appear between scans.
/// </summary>
public static class JpgReader
{
    // JPEG markers
    private const byte MarkerPrefix = 0xFF;
    private const byte SOI = 0xD8;
    private const byte EOI = 0xD9;
    private const byte SOS = 0xDA;
    private const byte DQT = 0xDB;
    private const byte DHT = 0xC4;
    private const byte DNL = 0xDC;
    private const byte DRI = 0xDD;
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
    /// Parses a JPEG header from <paramref name="bytes"/>, collecting frame, component, quantization,
    /// Huffman, and APP metadata up to and including the first SOS marker.
    /// </summary>
    /// <param name="bytes">Complete JPEG byte span.</param>
    /// <returns>
    /// Populated <see cref="JpgHeader"/> with <see cref="JpgHeader.ContentOffset"/> pointing at the entropy-coded data.
    /// </returns>
    public static JpgHeader ParseHeader(in ReadOnlySpan<byte> bytes)
    {
        JpgHeader header = new();
        JpgSpanReader reader = new(bytes);

        if (reader.ReadByte() != MarkerPrefix || reader.ReadByte() != SOI)
        {
            throw new InvalidDataException("Invalid JPEG: missing SOI marker");
        }

        while (!reader.EndOfSpan)
        {
            byte prefix = reader.ReadToNextMarker();
            if (prefix != MarkerPrefix)
            {
                throw new InvalidDataException("Invalid JPEG: expected marker prefix 0xFF");
            }

            byte marker = reader.ReadByte();
            if (marker == EOI)
            {
                break;
            }

            if (marker == SOS)
            {
                ushort len = reader.ReadUInt16BE();
                int sosPayload = len - 2;
                if (sosPayload < 0 || reader.Remaining < sosPayload)
                {
                    throw new InvalidDataException("Invalid SOS length");
                }

                JpgScanSpec scan = ParseSos(reader.ReadBytes(sosPayload));
                header.Scans.Add(scan);

                header.ContentOffset = reader.Position;

                // After the first SOS header, scan the remainder for DQT/DHT/DRI that may appear between scans
                CollectTablesAfterSos(bytes, header.ContentOffset, header);
                break;
            }

            if (marker == SOI)
            {
                continue;
            }

            ushort length = reader.ReadUInt16BE();
            if (length < 2 || reader.Remaining < length - 2)
            {
                throw new InvalidDataException("Invalid JPEG: segment length out of range");
            }

            int payloadLen = length - 2;
            ReadOnlySpan<byte> payload = reader.ReadBytes(payloadLen);

            if (marker == DQT)
            {
                header.HasQuantizationTables = true;
                List<JpgQuantizationTable> qTables = JpgQuantizationTable.ParseDqtPayload(payload);
                header.QuantizationTables.AddRange(qTables);
            }
            else if (marker == DHT)
            {
                header.HasHuffmanTables = true;
                List<JpgHuffmanTable> hTables = JpgHuffmanTable.ParseDhtPayload(payload);
                header.HuffmanTables.AddRange(hTables);
            }
            else if (marker == DRI)
            {
                if (payloadLen >= 2)
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

        return header;
    }

    private static void CollectTablesAfterSos(in ReadOnlySpan<byte> bytes, int start, JpgHeader header)
    {
        int i = start;
        while (i + 1 < bytes.Length)
        {
            // Find marker prefix 0xFF
            if (bytes[i++] != 0xFF)
            {
                continue;
            }

            // Skip fill 0xFF bytes
            while (i < bytes.Length && bytes[i] == 0xFF)
            {
                i++;
            }

            if (i >= bytes.Length)
            {
                break;
            }

            byte code = bytes[i++];
            if (code == 0x00)
            {
                // Stuffed 0x00: not a marker
                continue;
            }

            if (code >= 0xD0 && code <= 0xD7)
            {
                // RSTn: no payload
                continue;
            }

            if (code == EOI)
            {
                break;
            }

            if (code == SOS)
            {
                // Next scan begins; multi-scan layout detected.
                header.HasMultipleScans = true;
                break;
            }

            if (i + 2 > bytes.Length)
            {
                break;
            }

            var segLen = (ushort)(bytes[i] << 8 | bytes[i + 1]);
            i += 2;
            int payloadLen = segLen - 2;
            if (payloadLen < 0 || i + payloadLen > bytes.Length)
            {
                break;
            }

            ReadOnlySpan<byte> payload = bytes.Slice(i, payloadLen);

            switch (code)
            {
                case DQT:
                    {
                        header.HasQuantizationTables = true;
                        header.QuantizationTables.AddRange(JpgQuantizationTable.ParseDqtPayload(payload));
                        break;
                    }
                case DHT:
                    {
                        header.HasHuffmanTables = true;
                        header.HuffmanTables.AddRange(JpgHuffmanTable.ParseDhtPayload(payload));
                        break;
                    }
                case DRI:
                    {
                        if (payloadLen >= 2)
                        {
                            header.RestartInterval = ReadUInt16BE(payload);
                        }

                        break;
                    }
                case DNL:
                    {
                        // The number of lines carried here supersedes the one in the SOF segment.
                        if (payloadLen >= 2)
                        {
                            int numberOfLines = ReadUInt16BE(payload);
                            if (numberOfLines > 0)
                            {
                                header.Height = numberOfLines;
                            }
                        }

                        break;
                    }
                case APP14:
                    {
                        ParseApp14Adobe(payload, header);
                        break;
                    }
                case APP0:
                    {
                        ParseApp0(payload, header);
                        break;
                    }
                case APP1:
                    {
                        ParseApp1(payload, header);
                        break;
                    }
                case APP2:
                    {
                        ParseApp2Icc(payload, header);
                        break;
                    }
            }

            i += payloadLen;
        }
    }

    /// <summary>
    /// Parses an SOS (Start of Scan) segment payload into a <see cref="JpgScanSpec"/>.
    /// </summary>
    /// <param name="payload">Segment payload bytes (excluding the 2-byte length field).</param>
    /// <returns>Populated <see cref="JpgScanSpec"/> with component selectors and spectral/successive-approximation parameters.</returns>
    public static JpgScanSpec ParseSos(in ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1)
        {
            throw new InvalidDataException("Invalid SOS: too short");
        }

        int ns = payload[0];
        int offset = 1;
        JpgScanSpec scan = new();
        for (int i = 0; i < ns; i++)
        {
            if (offset + 2 >= payload.Length)
            {
                throw new InvalidDataException("Invalid SOS: component selector truncated");
            }

            byte cs = payload[offset + 0];
            byte tdTa = payload[offset + 1];
            offset += 2;

            scan.Components.Add(new JpgScanComponentSpec
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

    private static void ParseSof(byte marker, in ReadOnlySpan<byte> payload, JpgHeader header)
    {
        if (payload.Length < 6)
        {
            throw new InvalidDataException("Invalid SOF: too short");
        }

        header.FrameType = (JpgFrameType)marker;
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

            JpgComponent comp = new()
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

    private static void ParseApp0(in ReadOnlySpan<byte> payload, JpgHeader header)
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

    private static void ParseApp1(in ReadOnlySpan<byte> payload, JpgHeader header)
    {
        if (payload.Length >= 6 && payload[0] == (byte)'E' && payload[1] == (byte)'x' && payload[2] == (byte)'i' && payload[3] == (byte)'f' && payload[4] == 0 && payload[5] == 0)
        {
            header.IsExif = true;
        }
    }

    private static void ParseApp2Icc(in ReadOnlySpan<byte> payload, JpgHeader header)
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
                header.IccProfileSegments.Add(new IccSegmentInfo
                {
                    SequenceNumber = seq,
                    TotalSegments = total,
                    Data = data
                });
            }
        }
    }

    private static void ParseApp14Adobe(in ReadOnlySpan<byte> payload, JpgHeader header)
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

    private static ushort ReadUInt16BE(in ReadOnlySpan<byte> span) => (ushort)(span[0] << 8 | span[1]);
}
