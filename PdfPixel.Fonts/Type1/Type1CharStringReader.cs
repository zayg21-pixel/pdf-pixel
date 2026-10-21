using PdfPixel.Fonts.Cff;
using System;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Reads Type1 charstring data one value at a time. Operand encoding overlaps with Type2 charstrings
/// for the shared byte ranges (32-254), but byte 255 introduces a plain 32-bit integer here, not a
/// 16.16 fixed-point real as in Type2, and there is no 3-byte (28) short-int form. Operators occupy the
/// same 0-31 range with a different meaning set (<c>hsbw</c>, <c>closepath</c>, <c>callothersubr</c>,
/// <c>seac</c>, <c>sbw</c>, <c>div</c>, and no hint-replacement masks).
/// </summary>
internal ref struct Type1CharStringReader
{
    private readonly ReadOnlySpan<byte> _data;

    public Type1CharStringReader(in ReadOnlySpan<byte> data)
    {
        _data = data;
        Position = 0;
    }

    /// <summary>
    /// Gets or sets the current absolute read position within the data.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets a value indicating whether the reader has reached the end of the data.
    /// </summary>
    public readonly bool IsAtEnd => Position >= _data.Length;

    /// <summary>
    /// Reads the next value (operand or operator) from the charstring data.
    /// </summary>
    /// <returns>True if a value was read, false if the end of the data has been reached or the data is malformed.</returns>
    public bool TryReadNextValue(out CffValue value)
    {
        value = default;

        if (!TryReadByte(out byte firstByte))
        {
            return false;
        }

        if (firstByte == CffConstants.DictOperatorEscape)
        {
            if (!TryReadByte(out byte secondByte))
            {
                return false;
            }

            value = new CffValue(CffValueType.EscapedOperator, secondByte);
            return true;
        }

        if (firstByte <= CffConstants.CharStringOperatorMax)
        {
            value = new CffValue(CffValueType.Operator, firstByte);
            return true;
        }

        if (firstByte >= CffConstants.OperandIntLow && firstByte <= CffConstants.OperandIntHigh)
        {
            value = new CffValue(CffValueType.Integer, firstByte - CffConstants.SingleByteIntegerBias);
            return true;
        }

        if (firstByte >= CffConstants.OperandPositiveIntStart && firstByte <= CffConstants.OperandPositiveIntEnd)
        {
            if (!TryReadByte(out byte nextByte))
            {
                return false;
            }

            int integer = ((firstByte - CffConstants.OperandPositiveIntStart) << 8) + nextByte + CffConstants.TwoByteIntegerBias;
            value = new CffValue(CffValueType.Integer, integer);
            return true;
        }

        if (firstByte >= CffConstants.OperandNegativeIntStart && firstByte <= CffConstants.OperandNegativeIntEnd)
        {
            if (!TryReadByte(out byte nextByte))
            {
                return false;
            }

            int integer = (-(firstByte - CffConstants.OperandNegativeIntStart) << 8) - nextByte - CffConstants.TwoByteIntegerBias;
            value = new CffValue(CffValueType.Integer, integer);
            return true;
        }

        if (firstByte == CffConstants.CharStringOperandFixed) // 255: plain 32-bit integer in Type1 (not a 16.16 fixed-point real as in Type2).
        {
            if (!TryReadByte(out byte firstOctet)
                || !TryReadByte(out byte secondOctet)
                || !TryReadByte(out byte thirdOctet)
                || !TryReadByte(out byte fourthOctet))
            {
                return false;
            }

            int integer = (firstOctet << 24) | (secondOctet << 16) | (thirdOctet << 8) | fourthOctet;
            value = new CffValue(CffValueType.Integer, integer);
            return true;
        }

        return false;
    }

    private bool TryReadByte(out byte value)
    {
        if (IsAtEnd)
        {
            value = 0;
            return false;
        }

        value = _data[Position++];
        return true;
    }
}
