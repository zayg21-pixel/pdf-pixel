using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Low-level writer over raw CFF binary data: bytes, INDEX structures, and DICT operands. Used by
/// <see cref="CffTypefaceWriter"/> to assemble the pieces of a CFF font into their final byte layout.
/// </summary>
internal sealed class CffBinaryWriter
{
    private readonly List<byte> _output = [];

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public int Length => _output.Count;

    public void WriteByte(byte value) => _output.Add(value);

    public void WriteBytes(in ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            _output.Add(value);
        }
    }

    /// <summary>
    /// Writes the fixed 4-byte CFF header (major 1, minor 0, header size 4, offSize 1).
    /// </summary>
    public void WriteHeader()
    {
        WriteByte(1);
        WriteByte(0);
        WriteByte(4);
        WriteByte(1);
    }

    /// <summary>
    /// Writes a CFF INDEX structure from a list of entries.
    /// </summary>
    public void WriteIndex(IReadOnlyList<ReadOnlyMemory<byte>> entries)
    {
        WriteUInt16BE((ushort)entries.Count);
        if (entries.Count == 0)
        {
            return;
        }

        var offsets = new int[entries.Count + 1];
        offsets[0] = 1;
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            offsets[entryIndex + 1] = offsets[entryIndex] + entries[entryIndex].Length;
        }

        byte offSize = ComputeOffSize(offsets[entries.Count]);
        WriteByte(offSize);
        foreach (int offset in offsets)
        {
            WriteOffsetValue(offset, offSize);
        }

        foreach (ReadOnlyMemory<byte> entry in entries)
        {
            WriteBytes(entry.Span);
        }
    }

    public void WriteDictOperator(byte operatorCode) => WriteByte(operatorCode);

    public void WriteDictEscapedOperator(byte operatorCode)
    {
        WriteByte(CffConstants.DictOperatorEscape);
        WriteByte(operatorCode);
    }

    /// <summary>
    /// Writes a DICT operand, choosing the integer or real encoding based on the value.
    /// </summary>
    public void WriteDictNumber(float value)
    {
        bool isInteger = value == MathF.Round(value) && value >= int.MinValue && value <= int.MaxValue;
        if (isInteger)
        {
            WriteDictInteger((int)value);
        }
        else
        {
            WriteDictReal(value);
        }
    }

    /// <summary>
    /// Writes a DICT offset operand, always as a 32-bit long-int (marker 29 + 4 bytes) regardless of
    /// value. A DICT's own byte length then never depends on the numeric offset it ends up
    /// containing, which is what lets <see cref="CffTypefaceWriter"/> resolve every offset in a
    /// single pass instead of iterating to a fixed point.
    /// </summary>
    public void WriteDictOffset(int value)
    {
        WriteByte(CffConstants.OperandLongInt);
        WriteByte((byte)((value >> 24) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)(value & 0xFF));
    }

    public byte[] ToArray() => _output.ToArray();

    private void WriteDictInteger(int value)
    {
        if (value >= -107 && value <= 107)
        {
            WriteByte((byte)(value + CffConstants.SingleByteIntegerBias));
            return;
        }

        if (value >= 108 && value <= 1131)
        {
            int adjusted = value - CffConstants.TwoByteIntegerBias;
            WriteByte((byte)(CffConstants.OperandPositiveIntStart + (adjusted >> 8)));
            WriteByte((byte)(adjusted & 0xFF));
            return;
        }

        if (value >= -1131 && value <= -108)
        {
            int adjusted = -value - CffConstants.TwoByteIntegerBias;
            WriteByte((byte)(CffConstants.OperandNegativeIntStart + (adjusted >> 8)));
            WriteByte((byte)(adjusted & 0xFF));
            return;
        }

        if (value >= -32768 && value <= 32767)
        {
            WriteByte(CffConstants.OperandShortInt);
            WriteByte((byte)((value >> 8) & 0xFF));
            WriteByte((byte)(value & 0xFF));
            return;
        }

        WriteDictOffset(value);
    }

    private void WriteDictReal(float value)
    {
        WriteByte(CffConstants.OperandRealNumber);

        string floatString = value.ToString("G", CultureInfo.InvariantCulture);
        List<byte> nibbles = [];
        int index = 0;
        while (index < floatString.Length)
        {
            char character = floatString[index];
            if (character is >= '0' and <= '9')
            {
                nibbles.Add((byte)(character - '0'));
                index++;
            }
            else if (character == '.')
            {
                nibbles.Add(CffConstants.RealNibbleDecimalPoint);
                index++;
            }
            else if (character == 'E')
            {
                bool isNegativeExponent = (index + 1 < floatString.Length) && floatString[index + 1] == '-';
                bool isExplicitPositiveExponent = (index + 1 < floatString.Length) && floatString[index + 1] == '+';
                nibbles.Add(isNegativeExponent ? CffConstants.RealNibbleNegativeExponent : CffConstants.RealNibblePositiveExponent);
                index += (isNegativeExponent || isExplicitPositiveExponent) ? 2 : 1;
            }
            else if (character == '-')
            {
                nibbles.Add(CffConstants.RealNibbleMinus);
                index++;
            }
            else
            {
                index++;
            }
        }

        nibbles.Add(CffConstants.RealNibbleTerminator);

        for (int nibbleIndex = 0; nibbleIndex < nibbles.Count; nibbleIndex += 2)
        {
            byte highNibble = nibbles[nibbleIndex];
            byte lowNibble = (nibbleIndex + 1 < nibbles.Count) ? nibbles[nibbleIndex + 1] : CffConstants.RealNibbleTerminator;
            WriteByte((byte)((highNibble << 4) | lowNibble));
        }
    }

    private void WriteOffsetValue(int value, byte size)
    {
        for (int shiftIndex = size - 1; shiftIndex >= 0; shiftIndex--)
        {
            WriteByte((byte)((value >> (shiftIndex * 8)) & 0xFF));
        }
    }

    private void WriteUInt16BE(ushort value)
    {
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)(value & 0xFF));
    }

    private static byte ComputeOffSize(int maxOffset)
    {
        if (maxOffset <= 0xFF)
        {
            return 1;
        }

        if (maxOffset <= 0xFFFF)
        {
            return 2;
        }

        return (maxOffset <= 0xFFFFFF) ? (byte)3 : (byte)4;
    }
}
