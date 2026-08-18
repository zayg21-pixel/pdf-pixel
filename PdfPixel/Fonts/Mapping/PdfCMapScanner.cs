using System;
using System.Text;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Extractor for CMap character mapping ranges. Locates the standard begin/end range table blocks
/// directly in the raw CMap byte stream and reads their fixed row shapes (hex string / number pairs
/// and triples) without executing the stream as PostScript.
/// </summary>
public static class PdfCMapScanner
{
    // A character code is at most four bytes (ISO 32000-1 9.7.6.2); a bfchar destination is at most
    // 512 bytes of UTF-16BE (9.10.3). Longer hex strings are truncated to what fits.
    private const int MaxCodeLength = 4;
    private const int MaxDestinationLength = 512;

    private static ReadOnlySpan<byte> BeginCodespaceRange => "begincodespacerange"u8;

    private static ReadOnlySpan<byte> BeginBfChar => "beginbfchar"u8;

    private static ReadOnlySpan<byte> BeginBfRange => "beginbfrange"u8;

    private static ReadOnlySpan<byte> BeginCidChar => "begincidchar"u8;

    private static ReadOnlySpan<byte> BeginCidRange => "begincidrange"u8;

    private static ReadOnlySpan<byte> UseCMapOperator => "usecmap"u8;

    private static ReadOnlySpan<byte> CMapNameEntry => "CMapName"u8;

    private static ReadOnlySpan<byte> WModeEntry => "WMode"u8;

    private static ReadOnlySpan<byte> CidSystemInfoEntry => "CIDSystemInfo"u8;

    private static ReadOnlySpan<byte> RegistryEntry => "Registry"u8;

    private static ReadOnlySpan<byte> OrderingEntry => "Ordering"u8;

    private static ReadOnlySpan<byte> SupplementEntry => "Supplement"u8;

    /// <summary>
    /// Scans the raw CMap bytes for codespacerange, bfchar, bfrange, cidchar and cidrange tables,
    /// for the /CMapName, /WMode and /CIDSystemInfo entries, and for a usecmap reference resolved
    /// through <paramref name="cmapProvider"/>, and returns a <see cref="PdfCMap"/> populated from
    /// them.
    /// </summary>
    public static PdfCMap Scan(in ReadOnlyMemory<byte> cmapBytes, Func<PdfString, PdfCMap?> cmapProvider)
    {
        PdfCMap cmap = new();
        ReadOnlySpan<byte> data = cmapBytes.Span;
        int position = 0;
        int lastNameStart = 0;
        int lastNameLength = 0;
        var inCidSystemInfo = false;

        while (position < data.Length)
        {
            SkipWhitespace(data, ref position);

            if (position >= data.Length)
            {
                break;
            }

            if (data[position] == (byte)'/')
            {
                position++;
                lastNameStart = position;

                while (position < data.Length && IsRegular(data[position]))
                {
                    position++;
                }

                lastNameLength = position - lastNameStart;
                ReadOnlySpan<byte> name = data.Slice(lastNameStart, lastNameLength);

                if (name.SequenceEqual(WModeEntry))
                {
                    if (TryReadInteger(data, ref position, out int writingMode))
                    {
                        cmap.WMode = (CMapWMode)writingMode;
                    }
                }
                else if (name.SequenceEqual(CMapNameEntry))
                {
                    if (TryReadName(data, ref position, out ReadOnlySpan<byte> cmapName))
                    {
                        cmap.Name = (PdfString)cmapName;
                    }
                }
                else if (name.SequenceEqual(CidSystemInfoEntry))
                {
                    inCidSystemInfo = true;
                }
                else if (inCidSystemInfo)
                {
                    ReadCidSystemInfoEntry(data, ref position, name, cmap);
                }

                continue;
            }

            if (!IsRegular(data[position]))
            {
                position++;
                continue;
            }

            int tokenStart = position;

            while (position < data.Length && IsRegular(data[position]))
            {
                position++;
            }

            ReadOnlySpan<byte> token = data.Slice(tokenStart, position - tokenStart);

            if (token.SequenceEqual(BeginCodespaceRange))
            {
                ReadCodespaceRanges(data, ref position, cmap);
            }
            else if (token.SequenceEqual(BeginBfChar))
            {
                ReadBfChars(data, ref position, cmap);
            }
            else if (token.SequenceEqual(BeginBfRange))
            {
                ReadBfRanges(data, ref position, cmap);
            }
            else if (token.SequenceEqual(BeginCidChar))
            {
                ReadCidChars(data, ref position, cmap);
            }
            else if (token.SequenceEqual(BeginCidRange))
            {
                ReadCidRanges(data, ref position, cmap);
            }
            else if (token.SequenceEqual(UseCMapOperator) && lastNameLength > 0)
            {
                PdfCMap? baseCMap = cmapProvider((PdfString)data.Slice(lastNameStart, lastNameLength));

                if (baseCMap != null)
                {
                    cmap.MergeFrom(baseCMap);
                }
            }
        }

        return cmap;
    }

    private static void ReadCidSystemInfoEntry(in ReadOnlySpan<byte> data, ref int position, in ReadOnlySpan<byte> name, PdfCMap cmap)
    {
        if (name.SequenceEqual(RegistryEntry))
        {
            if (TryReadLiteralString(data, ref position, out ReadOnlySpan<byte> registry))
            {
                GetCidSystemInfo(cmap).Registry = (PdfString)registry;
            }

            return;
        }

        if (name.SequenceEqual(OrderingEntry))
        {
            if (TryReadLiteralString(data, ref position, out ReadOnlySpan<byte> ordering))
            {
                GetCidSystemInfo(cmap).Ordering = (PdfString)ordering;
            }

            return;
        }

        if (name.SequenceEqual(SupplementEntry))
        {
            if (TryReadInteger(data, ref position, out int supplement))
            {
                GetCidSystemInfo(cmap).Supplement = supplement;
            }
        }
    }

    private static PdfCidSystemInfo GetCidSystemInfo(PdfCMap cmap)
    {
        if (cmap.CidSystemInfo == null)
        {
            cmap.CidSystemInfo = new PdfCidSystemInfo();
        }

        return cmap.CidSystemInfo;
    }

    private static void ReadCodespaceRanges(in ReadOnlySpan<byte> data, ref int position, PdfCMap cmap)
    {
        Span<byte> startCode = stackalloc byte[MaxCodeLength];
        Span<byte> endCode = stackalloc byte[MaxCodeLength];

        while (TryReadHexString(data, ref position, startCode, out int startLength)
            && TryReadHexString(data, ref position, endCode, out int endLength))
        {
            cmap.AddCodespaceRange(startCode.Slice(0, startLength), endCode.Slice(0, endLength));
        }
    }

    private static void ReadBfChars(in ReadOnlySpan<byte> data, ref int position, PdfCMap cmap)
    {
        Span<byte> code = stackalloc byte[MaxCodeLength];
        Span<byte> destination = stackalloc byte[MaxDestinationLength];

        while (TryReadHexString(data, ref position, code, out int codeLength)
            && TryReadHexString(data, ref position, destination, out int destinationLength))
        {
            AddUnicodeMapping(cmap, code.Slice(0, codeLength), destination.Slice(0, destinationLength));
        }
    }

    private static void ReadBfRanges(in ReadOnlySpan<byte> data, ref int position, PdfCMap cmap)
    {
        Span<byte> startCode = stackalloc byte[MaxCodeLength];
        Span<byte> endCode = stackalloc byte[MaxCodeLength];
        Span<byte> code = stackalloc byte[MaxCodeLength];
        Span<byte> destination = stackalloc byte[MaxDestinationLength];

        while (TryReadHexString(data, ref position, startCode, out int startLength)
            && TryReadHexString(data, ref position, endCode, out int endLength))
        {
            SkipWhitespace(data, ref position);

            if (position < data.Length && data[position] == (byte)'[')
            {
                position++;
                uint currentCode = PdfCharacterCode.UnpackBigEndianToUInt(startCode.Slice(0, startLength));

                while (TryReadHexString(data, ref position, destination, out int destinationLength))
                {
                    WriteBigEndian(currentCode, code.Slice(0, startLength));
                    AddUnicodeMapping(cmap, code.Slice(0, startLength), destination.Slice(0, destinationLength));
                    currentCode++;
                }

                SkipWhitespace(data, ref position);

                if (position < data.Length && data[position] == (byte)']')
                {
                    position++;
                }
            }
            else if (TryReadHexString(data, ref position, destination, out int destinationLength))
            {
                AddUnicodeRangeMapping(
                    cmap,
                    startCode.Slice(0, startLength),
                    endCode.Slice(0, endLength),
                    destination.Slice(0, destinationLength));
            }
        }
    }

    private static void ReadCidChars(in ReadOnlySpan<byte> data, ref int position, PdfCMap cmap)
    {
        Span<byte> code = stackalloc byte[MaxCodeLength];

        while (TryReadHexString(data, ref position, code, out int codeLength)
            && TryReadInteger(data, ref position, out int cid))
        {
            cmap.AddCidMapping(new PdfCharacterCode(code.Slice(0, codeLength).ToArray()), cid);
        }
    }

    private static void ReadCidRanges(in ReadOnlySpan<byte> data, ref int position, PdfCMap cmap)
    {
        Span<byte> startCode = stackalloc byte[MaxCodeLength];
        Span<byte> endCode = stackalloc byte[MaxCodeLength];

        while (TryReadHexString(data, ref position, startCode, out int startLength)
            && TryReadHexString(data, ref position, endCode, out int endLength)
            && TryReadInteger(data, ref position, out int firstCid))
        {
            cmap.AddCidRangeMapping(startCode.Slice(0, startLength), endCode.Slice(0, endLength), firstCid);
        }
    }

    private static void AddUnicodeMapping(PdfCMap cmap, in ReadOnlySpan<byte> code, in ReadOnlySpan<byte> destination)
    {
        if (IsSpecialsBlockSentinel(destination))
        {
            return;
        }

        cmap.AddMapping(new PdfCharacterCode(code.ToArray()), DecodeUtf16BE(destination));
    }

    private static void AddUnicodeRangeMapping(PdfCMap cmap, in ReadOnlySpan<byte> startCode, in ReadOnlySpan<byte> endCode, in ReadOnlySpan<byte> destination)
    {
        if (IsSpecialsBlockSentinel(destination))
        {
            return;
        }

        string unicode = DecodeUtf16BE(destination);
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

    private static bool IsSpecialsBlockSentinel(in ReadOnlySpan<byte> bytes) => bytes.Length == 2 && bytes[0] == 0xFF && bytes[1] >= 0xF0;

    private static string DecodeUtf16BE(in ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> span = bytes;

        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            span = span.Slice(2);
        }

        return PdfPixel.Text.EncodingExtensions.GetString(Encoding.BigEndianUnicode, span);
    }

    private static void WriteBigEndian(uint value, in Span<byte> destination)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    private static bool TryReadHexString(in ReadOnlySpan<byte> data, ref int position, in Span<byte> destination, out int length)
    {
        SkipWhitespace(data, ref position);
        length = 0;

        if (position >= data.Length || data[position] != (byte)'<')
        {
            return false;
        }

        position++;

        int highNibble = -1;

        while (position < data.Length && data[position] != (byte)'>')
        {
            int nibble = HexCharToInt(data[position]);

            if (nibble >= 0)
            {
                if (highNibble < 0)
                {
                    highNibble = nibble;
                }
                else
                {
                    if (length < destination.Length)
                    {
                        destination[length] = (byte)((highNibble << 4) | nibble);
                        length++;
                    }

                    highNibble = -1;
                }
            }

            position++;
        }

        if (position < data.Length)
        {
            position++;
        }

        if (highNibble >= 0 && length < destination.Length)
        {
            destination[length] = (byte)(highNibble << 4);
            length++;
        }

        return true;
    }

    private static bool TryReadName(in ReadOnlySpan<byte> data, ref int position, out ReadOnlySpan<byte> value)
    {
        SkipWhitespace(data, ref position);

        if (position >= data.Length || data[position] != (byte)'/')
        {
            value = default;
            return false;
        }

        position++;
        int start = position;

        while (position < data.Length && IsRegular(data[position]))
        {
            position++;
        }

        value = data.Slice(start, position - start);

        return !value.IsEmpty;
    }

    private static bool TryReadLiteralString(in ReadOnlySpan<byte> data, ref int position, out ReadOnlySpan<byte> value)
    {
        SkipWhitespace(data, ref position);

        if (position >= data.Length || data[position] != (byte)'(')
        {
            value = default;
            return false;
        }

        position++;
        int start = position;
        int depth = 1;

        while (position < data.Length)
        {
            byte current = data[position];

            if (current == (byte)'\\')
            {
                position += 2;
                continue;
            }

            if (current == (byte)'(')
            {
                depth++;
            }
            else if (current == (byte)')')
            {
                depth--;

                if (depth == 0)
                {
                    break;
                }
            }

            position++;
        }

        int end = (position < data.Length) ? position : data.Length;
        value = data.Slice(start, end - start);

        if (position < data.Length)
        {
            position++;
        }

        return true;
    }

    private static bool TryReadInteger(in ReadOnlySpan<byte> data, ref int position, out int value)
    {
        SkipWhitespace(data, ref position);
        value = 0;
        int start = position;
        var negative = false;

        if (position < data.Length && (data[position] == (byte)'+' || data[position] == (byte)'-'))
        {
            negative = data[position] == (byte)'-';
            position++;
        }

        int digitsStart = position;
        long parsed = 0;

        while (position < data.Length && data[position] >= (byte)'0' && data[position] <= (byte)'9')
        {
            parsed = (parsed * 10) + (data[position] - (byte)'0');

            if (parsed > int.MaxValue)
            {
                position = start;
                return false;
            }

            position++;
        }

        if (position == digitsStart)
        {
            position = start;
            return false;
        }

        value = (int)(negative ? -parsed : parsed);

        return true;
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

    private static bool IsRegular(byte value)
    {
        if (IsPsWhitespace(value))
        {
            return false;
        }

        return value != (byte)'('
            && value != (byte)')'
            && value != (byte)'<'
            && value != (byte)'>'
            && value != (byte)'['
            && value != (byte)']'
            && value != (byte)'{'
            && value != (byte)'}'
            && value != (byte)'/'
            && value != (byte)'%';
    }

    private static int HexCharToInt(byte value)
    {
        if (value >= (byte)'0' && value <= (byte)'9')
        {
            return value - (byte)'0';
        }

        if (value >= (byte)'A' && value <= (byte)'F')
        {
            return value - (byte)'A' + 10;
        }

        if (value >= (byte)'a' && value <= (byte)'f')
        {
            return value - (byte)'a' + 10;
        }

        return -1;
    }
}
