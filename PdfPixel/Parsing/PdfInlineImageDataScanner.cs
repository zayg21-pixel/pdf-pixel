using PdfPixel.Streams;
using System;

namespace PdfPixel.Parsing;

/// <summary>
/// Finds where the data of an inline image ends in a content stream, and the EI operator that follows it. Offsets are relative
/// to the start of the data searched, the bytes that follow the ID operator.
/// </summary>
internal static class PdfInlineImageDataScanner
{
    /// <summary>
    /// Finds the data of an inline image in <paramref name="content"/>, the bytes that follow its ID operator: through the
    /// end-of-data marker of <paramref name="filter"/>, or for unfiltered data <paramref name="rawDataLength"/> bytes when known,
    /// otherwise up to the EI operator.
    /// </summary>
    /// <param name="content">The content stream from right after the ID operator to its end.</param>
    /// <param name="filter">The first filter applied to the data, or null when the data is not filtered.</param>
    /// <param name="rawDataLength">The length of unfiltered data computed from the image entries, or null when unknown.</param>
    /// <param name="dataStart">Offset of the first data byte, past the single whitespace after the ID operator.</param>
    /// <param name="dataEnd">Offset past the last data byte.</param>
    /// <param name="operatorStart">Offset of the EI operator.</param>
    /// <returns>False when the end of the data, or the EI operator after it, is not found.</returns>
    public static bool TryFindData(in ReadOnlySpan<byte> content, PdfFilterType? filter, int? rawDataLength, out int dataStart, out int dataEnd, out int operatorStart)
    {
        // Skip single whitespace after ID per PDF spec.
        int position = 0;
        byte firstByte = PeekByte(content, position);
        if (firstByte == PdfParser.CarriageReturn)
        {
            position++;
            if (position < content.Length && PeekByte(content, position) == PdfParser.LineFeed)
            {
                position++;
            }
        }
        else if (position < content.Length && PdfParser.IsWhitespace(firstByte))
        {
            position++;
        }

        dataStart = position;

        bool endFound = filter switch
        {
            PdfFilterType.ASCII85Decode => TryReadUntilAscii85Eod(content, ref position, out dataEnd),
            PdfFilterType.ASCIIHexDecode => TryReadUntilAsciiHexEod(content, ref position, out dataEnd),
            PdfFilterType.DCTDecode => TryReadUntilJpegEoi(content, ref position, out dataEnd),
            null => TryReadRawImageData(content, ref position, rawDataLength, out dataEnd) || TryScanEi(content, ref position, out dataEnd),
            _ => TryScanEi(content, ref position, out dataEnd)
        };

        operatorStart = position;

        return endFound;
    }

    private static bool TryReadUntilAscii85Eod(in ReadOnlySpan<byte> content, ref int position, out int dataEnd)
    {
        while (position < content.Length)
        {
            byte current = content[position++];

            if (current != (byte)'~')
            {
                continue;
            }

            while (position < content.Length && PdfParser.IsWhitespace(content[position]))
            {
                position++;
            }

            if (position < content.Length && content[position] == (byte)'>')
            {
                position++;
            }

            dataEnd = position;
            return SkipToEiOperator(content, ref position);
        }

        dataEnd = position;
        return false;
    }

    private static bool TryReadUntilAsciiHexEod(in ReadOnlySpan<byte> content, ref int position, out int dataEnd)
    {
        while (position < content.Length)
        {
            byte current = content[position++];

            if (current == (byte)'>')
            {
                dataEnd = position;
                return SkipToEiOperator(content, ref position);
            }
        }

        dataEnd = position;
        return false;
    }

    /// <summary>
    /// Walks JPEG markers to find the EOI marker (0xFFD9) instead of scanning raw bytes for it, since
    /// length-prefixed segments such as APPn (which may embed a full nested JPEG thumbnail) can otherwise
    /// contain a coincidental 0xFFD9 byte pair that is not the real end of the image.
    /// </summary>
    private static bool TryReadUntilJpegEoi(in ReadOnlySpan<byte> content, ref int position, out int dataEnd)
    {
        dataEnd = position;

        while (position < content.Length)
        {
            byte current = content[position++];

            if (current != (byte)0xFF || position >= content.Length)
            {
                continue;
            }

            byte marker = content[position];

            if (marker == 0x00)
            {
                // Byte-stuffed literal 0xFF within entropy-coded scan data, not a marker.
                position++;
                continue;
            }

            if (marker == (byte)0xFF)
            {
                // Fill byte padding before the real marker; re-check it as the next prefix candidate.
                continue;
            }

            if (marker == (byte)0xD9)
            {
                position++;
                dataEnd = position;
                return SkipToEiOperator(content, ref position);
            }

            if ((marker >= 0xD0 && marker <= 0xD8) || marker == 0x01)
            {
                // Standalone marker (RST0-7, SOI, TEM): no length-prefixed payload follows.
                position++;
                continue;
            }

            position++;

            if (position + 1 >= content.Length)
            {
                return false;
            }

            int segmentLength = (content[position] << 8) | content[position + 1];
            if (segmentLength < 2)
            {
                return false;
            }

            if (position + segmentLength > content.Length)
            {
                return false;
            }

            position += segmentLength;
        }

        return false;
    }

    /// <summary>
    /// Advances past whitespace and returns whether an EI operator starts at the resulting position, which is left on it.
    /// </summary>
    private static bool SkipToEiOperator(in ReadOnlySpan<byte> content, ref int position)
    {
        while (position < content.Length && PdfParser.IsWhitespace(content[position]))
        {
            position++;
        }

        if (position + 1 >= content.Length || content[position] != (byte)'E' || content[position + 1] != (byte)'I')
        {
            return false;
        }

        return (position + 2 >= content.Length) || PdfParser.IsTokenTerminator(content[position + 2]);
    }

    /// <summary>
    /// Reads exactly the number of data bytes computed from /Width, /Height, /BitsPerComponent and /ColorSpace
    /// (or /ImageMask) when the image is unfiltered. Unfiltered inline image data is arbitrary binary and may
    /// coincidentally contain a whitespace-delimited "EI" sequence, so the length must be computed instead of
    /// found by scanning.
    /// </summary>
    private static bool TryReadRawImageData(in ReadOnlySpan<byte> content, ref int position, int? dataLength, out int dataEnd)
    {
        int dataStart = position;
        dataEnd = dataStart;

        if (dataLength == null || dataStart + dataLength.Value > content.Length)
        {
            return false;
        }

        position += dataLength.Value;
        dataEnd = position;

        if (!SkipToEiOperator(content, ref position))
        {
            position = dataStart;
            return false;
        }

        return true;
    }

    private static bool TryScanEi(in ReadOnlySpan<byte> content, ref int position, out int dataEnd)
    {
        int previousByte = -1;

        while (position < content.Length)
        {
            byte current = content[position++];

            if (current == (byte)'E' && position < content.Length && content[position] == (byte)'I')
            {
                bool precedingWhitespace = previousByte == -1 || PdfParser.IsWhitespace((byte)previousByte);
                bool followingDelimiter = (position + 1 >= content.Length) || PdfParser.IsTokenTerminator(content[position + 1]);

                if (precedingWhitespace && followingDelimiter)
                {
                    position--;
                    dataEnd = position;
                    return true;
                }
            }

            previousByte = current;
        }

        dataEnd = position;
        return false;
    }

    private static byte PeekByte(in ReadOnlySpan<byte> content, int position) => (position < content.Length) ? content[position] : (byte)0;
}
