using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Fallback extractor for CMap character mapping ranges. Locates the standard
/// begin/end range table blocks directly in the raw CMap byte stream and reads their
/// fixed row shapes (hex string / number pairs and triples) without executing the
/// stream as PostScript. Used to recover mapping data when the surrounding
/// PostScript is malformed and the normal resource-based parse fails.
/// </summary>
internal static class PdfCMapScanner
{
    /// <summary>
    /// Scans the raw CMap bytes for codespacerange, bfchar, bfrange, cidchar and
    /// cidrange tables and returns a <see cref="PdfCMap"/> populated from them.
    /// </summary>
    public static PdfCMap Scan(in ReadOnlyMemory<byte> cmapBytes)
    {
        PdfCMap cmap = new();
        ReadOnlySpan<byte> data = cmapBytes.Span;

        ScanCodespaceRanges(data, cmap);
        ScanBfChar(data, cmap);
        ScanBfRange(data, cmap);
        ScanCidChar(data, cmap);
        ScanCidRange(data, cmap);

        return cmap;
    }

    private static void ScanCodespaceRanges(in ReadOnlySpan<byte> data, PdfCMap cmap)
    {
        foreach (BlockBounds block in FindBlocks(data, "begincodespacerange", "endcodespacerange"))
        {
            ReadOnlySpan<byte> content = data.Slice(block.Start, block.End - block.Start);
            int position = 0;
            while (TryReadHexString(content, ref position, out byte[] startCode) && TryReadHexString(content, ref position, out byte[] endCode))
            {
                cmap.AddCodespaceRange(startCode, endCode);
            }
        }
    }

    private static void ScanBfChar(in ReadOnlySpan<byte> data, PdfCMap cmap)
    {
        foreach (BlockBounds block in FindBlocks(data, "beginbfchar", "endbfchar"))
        {
            ReadOnlySpan<byte> content = data.Slice(block.Start, block.End - block.Start);
            int position = 0;
            while (TryReadHexString(content, ref position, out byte[] code) && TryReadHexString(content, ref position, out byte[] destination))
            {
                AddUnicodeMapping(cmap, code, destination);
            }
        }
    }

    private static void ScanBfRange(in ReadOnlySpan<byte> data, PdfCMap cmap)
    {
        foreach (BlockBounds block in FindBlocks(data, "beginbfrange", "endbfrange"))
        {
            ReadOnlySpan<byte> content = data.Slice(block.Start, block.End - block.Start);
            int position = 0;
            while (TryReadHexString(content, ref position, out byte[] startCode) && TryReadHexString(content, ref position, out byte[] endCode))
            {
                SkipWhitespace(content, ref position);
                if (position < content.Length && content[position] == (byte)'[')
                {
                    position++;
                    uint currentCode = PdfCharacterCode.UnpackBigEndianToUInt(startCode);
                    while (TryReadHexString(content, ref position, out byte[] destination))
                    {
                        byte[] codeBytes = PdfCharacterCode.PackUIntToBigEndian(currentCode, startCode.Length).ToArray();
                        AddUnicodeMapping(cmap, codeBytes, destination);
                        currentCode++;
                    }

                    SkipWhitespace(content, ref position);
                    if (position < content.Length && content[position] == (byte)']')
                    {
                        position++;
                    }
                }
                else if (TryReadHexString(content, ref position, out byte[] destinationBase))
                {
                    AddUnicodeRangeMapping(cmap, startCode, endCode, destinationBase);
                }
            }
        }
    }

    private static void ScanCidChar(in ReadOnlySpan<byte> data, PdfCMap cmap)
    {
        foreach (BlockBounds block in FindBlocks(data, "begincidchar", "endcidchar"))
        {
            ReadOnlySpan<byte> content = data.Slice(block.Start, block.End - block.Start);
            int position = 0;
            while (TryReadHexString(content, ref position, out byte[] code) && TryReadInteger(content, ref position, out int cid))
            {
                cmap.AddCidMapping(new PdfCharacterCode(code), cid);
            }
        }
    }

    private static void ScanCidRange(in ReadOnlySpan<byte> data, PdfCMap cmap)
    {
        foreach (BlockBounds block in FindBlocks(data, "begincidrange", "endcidrange"))
        {
            ReadOnlySpan<byte> content = data.Slice(block.Start, block.End - block.Start);
            int position = 0;
            while (TryReadHexString(content, ref position, out byte[] startCode)
                && TryReadHexString(content, ref position, out byte[] endCode)
                && TryReadInteger(content, ref position, out int firstCid))
            {
                cmap.AddCidRangeMapping(startCode, endCode, firstCid);
            }
        }
    }

    private static void AddUnicodeMapping(PdfCMap cmap, byte[] codeBytes, byte[] destinationBytes)
    {
        if (IsSentinelFFFF(destinationBytes) || IsSentinelFFFD(destinationBytes))
        {
            return;
        }

        cmap.AddMapping(new PdfCharacterCode(codeBytes), DecodeUtf16BE(destinationBytes));
    }

    private static void AddUnicodeRangeMapping(PdfCMap cmap, byte[] startCode, byte[] endCode, byte[] destinationBytes)
    {
        if (IsSentinelFFFF(destinationBytes) || IsSentinelFFFD(destinationBytes))
        {
            return;
        }

        string unicode = DecodeUtf16BE(destinationBytes);
        int baseScalar;
        if (unicode.Length == 1)
        {
            baseScalar = unicode[0];
        }
        else if (unicode.Length == 2 && char.IsSurrogatePair(unicode[0], unicode[1]))
        {
            baseScalar = char.ConvertToUtf32(unicode[0], unicode[1]);
        }
        else
        {
            // Multi-codepoint destination sequences are not representable as a linear range; skip.
            return;
        }

        cmap.AddRangeMapping(startCode, endCode, baseScalar);
    }

    private static bool IsSentinelFFFF(byte[] bytes) => bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFF;

    // Adobe's CID-to-Unicode source data uses <FFFD> as a "no Unicode value" sentinel for a CID.
    private static bool IsSentinelFFFD(byte[] bytes) => bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] == 0xFD;

    private static string DecodeUtf16BE(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            span = span.Slice(2);
        }

        return Encoding.BigEndianUnicode.GetString(span.ToArray());
    }

    private static List<BlockBounds> FindBlocks(in ReadOnlySpan<byte> data, string beginMarker, string endMarker)
    {
        List<BlockBounds> blocks = [];
        byte[] beginBytes = Encoding.ASCII.GetBytes(beginMarker);
        byte[] endBytes = Encoding.ASCII.GetBytes(endMarker);

        int searchPosition = 0;
        while (searchPosition < data.Length)
        {
            int beginIndex = data.Slice(searchPosition).IndexOf(beginBytes);
            if (beginIndex < 0)
            {
                break;
            }

            int contentStart = searchPosition + beginIndex + beginBytes.Length;
            int endIndex = data.Slice(contentStart).IndexOf(endBytes);
            if (endIndex < 0)
            {
                break;
            }

            int contentEnd = contentStart + endIndex;
            blocks.Add(new BlockBounds(contentStart, contentEnd));

            searchPosition = contentEnd + endBytes.Length;
        }

        return blocks;
    }

    private readonly struct BlockBounds
    {
        public BlockBounds(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }

    private static bool TryReadHexString(in ReadOnlySpan<byte> data, ref int position, out byte[] value)
    {
        SkipWhitespace(data, ref position);
        if (position >= data.Length || data[position] != (byte)'<')
        {
            value = Array.Empty<byte>();
            return false;
        }

        position++; // consume '<'

        List<byte> bytes = [];
        int highNibble = -1;
        while (position < data.Length && data[position] != (byte)'>')
        {
            int nibble = HexCharToInt((char)data[position]);
            if (nibble >= 0)
            {
                if (highNibble < 0)
                {
                    highNibble = nibble;
                }
                else
                {
                    bytes.Add((byte)((highNibble << 4) | nibble));
                    highNibble = -1;
                }
            }

            position++;
        }

        if (position < data.Length)
        {
            position++; // consume '>'
        }

        if (highNibble >= 0)
        {
            bytes.Add((byte)(highNibble << 4));
        }

        value = bytes.ToArray();
        return true;
    }

    private static bool TryReadInteger(in ReadOnlySpan<byte> data, ref int position, out int value)
    {
        SkipWhitespace(data, ref position);
        int start = position;
        if (position < data.Length && (data[position] == (byte)'+' || data[position] == (byte)'-'))
        {
            position++;
        }

        int digitsStart = position;
        while (position < data.Length && data[position] >= (byte)'0' && data[position] <= (byte)'9')
        {
            position++;
        }

        if (position == digitsStart)
        {
            position = start;
            value = 0;
            return false;
        }

        string raw = Encoding.ASCII.GetString(data.Slice(start, position - start).ToArray());
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void SkipWhitespace(in ReadOnlySpan<byte> data, ref int position)
    {
        while (position < data.Length)
        {
            byte current = data[position];
            if (IsPsWhitespace(current))
            {
                position++;
                continue;
            }

            if (current == (byte)'%')
            {
                position++;
                while (position < data.Length && data[position] != (byte)'\n' && data[position] != (byte)'\r')
                {
                    position++;
                }

                continue;
            }

            break;
        }
    }

    private static bool IsPsWhitespace(byte value) => value == 0x20 || value == 0x09 || value == 0x0D || value == 0x0A || value == 0x0C || value == 0x00;

    private static int HexCharToInt(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'A' && value <= 'F')
        {
            return value - 'A' + 10;
        }

        if (value >= 'a' && value <= 'f')
        {
            return value - 'a' + 10;
        }

        return -1;
    }
}
