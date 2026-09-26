using PdfPixel.Tiff.Model;
using System;
using System.IO;

namespace PdfPixel.Tiff.Decoding;

/// <summary>
/// Builds a complete JPEG stream for each chunk of an image in the original TIFF 6.0 JPEG scheme (compression 6).
/// </summary>
internal sealed class TiffOldJpegStreamBuilder
{
    private const int MarkerLength = 2;
    private const byte MarkerPrefix = 0xFF;
    private const byte StartOfImageMarker = 0xD8;
    private const byte EndOfImageMarker = 0xD9;
    private const byte BaselineFrameMarker = 0xC0;
    private const byte DefineHuffmanTableMarker = 0xC4;
    private const byte StartOfScanMarker = 0xDA;
    private const byte DefineQuantizationTableMarker = 0xDB;
    private const byte DefineRestartIntervalMarker = 0xDD;

    private static readonly int[] DefaultYCbCrSubsampling = [2, 2];

    private readonly TiffDirectory _directory;

    public TiffOldJpegStreamBuilder(TiffDirectory directory) => _directory = directory;

    /// <summary>
    /// Returns the chunk itself when it is a JPEG stream, the JPEGInterchangeFormat stream for a single-chunk image,
    /// the JPEGInterchangeFormat headers up to its scan followed by the chunk, or headers synthesized from the
    /// JPEGQTables, JPEGDCTables and JPEGACTables followed by the chunk.
    /// </summary>
    public byte[] Build(in ReadOnlySpan<byte> chunk, int chunkWidth, int chunkRows, int samplesPerPixel)
    {
        if (chunk.Length >= MarkerLength && chunk[0] == MarkerPrefix && chunk[1] == StartOfImageMarker)
        {
            return chunk.ToArray();
        }

        TiffOldJpegParameters parameters = _directory.OldJpeg
            ?? throw new InvalidDataException("TIFF old-style JPEG image has no JPEG tags.");

        using MemoryStream stream = new();

        if (parameters.InterchangeFormat != null)
        {
            // TODO: [LOW] Interchange streams holding tables only, with the frame and scan headers left to the other tags.
            int scanDataOffset = FindEndOfScanHeader(parameters.InterchangeFormat);

            long[]? chunkOffsets = _directory.TileOffsets ?? _directory.StripOffsets;
            bool isSingleChunk = chunkOffsets != null && chunkOffsets.Length == 1;

            // Single-chunk images: the stream is the whole image, and writers disagree on where the chunk offset points.
            int headerLength = isSingleChunk ? parameters.InterchangeFormat.Length : scanDataOffset;
            stream.Write(parameters.InterchangeFormat, 0, headerLength);

            if (!isSingleChunk)
            {
                stream.Write(chunk.ToArray(), 0, chunk.Length);
            }
        }
        else
        {
            WriteSynthesizedHeaders(stream, parameters, chunkWidth, chunkRows, samplesPerPixel);
            stream.Write(chunk.ToArray(), 0, chunk.Length);
        }

        stream.WriteByte(MarkerPrefix);
        stream.WriteByte(EndOfImageMarker);

        return stream.ToArray();
    }

    private void WriteSynthesizedHeaders(MemoryStream stream, TiffOldJpegParameters parameters, int chunkWidth, int chunkRows, int samplesPerPixel)
    {
        byte[][] quantizationTables = parameters.QuantizationTables ?? throw new InvalidDataException("TIFF old-style JPEG image has no JPEGQTables.");
        byte[][] dcTables = parameters.DcHuffmanTables ?? throw new InvalidDataException("TIFF old-style JPEG image has no JPEGDCTables.");
        byte[][] acTables = parameters.AcHuffmanTables ?? throw new InvalidDataException("TIFF old-style JPEG image has no JPEGACTables.");

        if (quantizationTables.Length < samplesPerPixel || dcTables.Length < samplesPerPixel || acTables.Length < samplesPerPixel)
        {
            throw new InvalidDataException($"TIFF old-style JPEG tables cover fewer than {samplesPerPixel} component(s).");
        }

        int lumaSampling = 0x11;
        if (_directory.Photometric == TiffPhotometric.YCbCr)
        {
            int[] subsampling = _directory.YCbCrSubsampling ?? DefaultYCbCrSubsampling;
            lumaSampling = (subsampling[0] << 4) | subsampling[1];
        }

        stream.WriteByte(MarkerPrefix);
        stream.WriteByte(StartOfImageMarker);

        for (int component = 0; component < samplesPerPixel; component++)
        {
            WriteSegment(stream, DefineQuantizationTableMarker, [(byte)component], quantizationTables[component]);
            WriteSegment(stream, DefineHuffmanTableMarker, [(byte)component], dcTables[component]);
            WriteSegment(stream, DefineHuffmanTableMarker, [(byte)(0x10 | component)], acTables[component]);
        }

        if (parameters.RestartInterval is int restartInterval && restartInterval > 0)
        {
            WriteSegment(stream, DefineRestartIntervalMarker, [(byte)(restartInterval >> 8), (byte)restartInterval], []);
        }

        var frame = new byte[6 + (samplesPerPixel * 3)];
        frame[0] = 8;
        frame[1] = (byte)(chunkRows >> 8);
        frame[2] = (byte)chunkRows;
        frame[3] = (byte)(chunkWidth >> 8);
        frame[4] = (byte)chunkWidth;
        frame[5] = (byte)samplesPerPixel;

        var scan = new byte[4 + (samplesPerPixel * 2)];
        scan[0] = (byte)samplesPerPixel;

        for (int component = 0; component < samplesPerPixel; component++)
        {
            frame[6 + (component * 3)] = (byte)(component + 1);
            frame[7 + (component * 3)] = (byte)((component == 0) ? lumaSampling : 0x11);
            frame[8 + (component * 3)] = (byte)component;

            scan[1 + (component * 2)] = (byte)(component + 1);
            scan[2 + (component * 2)] = (byte)((component << 4) | component);
        }

        int spectralSelection = 1 + (samplesPerPixel * 2);
        scan[spectralSelection] = 0;
        scan[spectralSelection + 1] = 63;
        scan[spectralSelection + 2] = 0;

        WriteSegment(stream, BaselineFrameMarker, frame, []);
        WriteSegment(stream, StartOfScanMarker, scan, []);
    }

    private static void WriteSegment(MemoryStream stream, byte marker, byte[] head, byte[] body)
    {
        int length = 2 + head.Length + body.Length;

        stream.WriteByte(MarkerPrefix);
        stream.WriteByte(marker);
        stream.WriteByte((byte)(length >> 8));
        stream.WriteByte((byte)length);
        stream.Write(head, 0, head.Length);
        stream.Write(body, 0, body.Length);
    }

    private static int FindEndOfScanHeader(byte[] interchangeFormat)
    {
        int position = MarkerLength;

        while (position + 4 <= interchangeFormat.Length)
        {
            if (interchangeFormat[position] != MarkerPrefix)
            {
                break;
            }

            byte marker = interchangeFormat[position + 1];
            if (marker == MarkerPrefix)
            {
                position++;
                continue;
            }

            int segmentEnd = position + MarkerLength + ((interchangeFormat[position + 2] << 8) | interchangeFormat[position + 3]);
            if (marker == StartOfScanMarker)
            {
                return Math.Min(segmentEnd, interchangeFormat.Length);
            }

            position = segmentEnd;
        }

        throw new NotSupportedException("TIFF old-style JPEG interchange stream has no scan header.");
    }
}
