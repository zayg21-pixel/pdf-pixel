using PdfPixel.Jpeg.Parsing;

using System;
using System.IO;

namespace PdfPixel.Jpeg;

public sealed partial class JpegHeader
{
    // JPEG markers
    private const byte MarkerPrefix = 0xFF;
    private const byte SOI = 0xD8;
    private const byte EOI = 0xD9;
    private const byte SOS = 0xDA;

    /// <summary>
    /// Parses a JPEG header from <paramref name="bytes"/>, collecting frame, component, quantization,
    /// Huffman, and APP metadata up to and including the first SOS marker.
    /// </summary>
    /// <param name="bytes">Complete JPEG byte span.</param>
    /// <returns>
    /// Populated <see cref="JpegHeader"/> with <see cref="ContentOffset"/> pointing at the entropy-coded data.
    /// </returns>
    public static JpegHeader Parse(in ReadOnlySpan<byte> bytes)
    {
        JpegHeader header = new();
        JpegSpanReader reader = new(bytes);

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

                JpegScanSpec scan = JpegSegmentParser.ParseSos(reader.ReadBytes(sosPayload));
                header.Scans.Add(scan);

                header.ContentOffset = reader.Position;

                if (header.FrameType == JpegFrameType.ExtendedSequentialDct)
                {
                    header.HasMultipleScans = JpegSegmentParser.HasFurtherScan(bytes, header.ContentOffset);
                }

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

            JpegSegmentParser.ParseSegment(marker, payload, header);
        }

        return header;
    }
}
