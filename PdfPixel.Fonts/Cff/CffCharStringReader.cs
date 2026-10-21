using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads Type2 charstring data one value at a time. Operand encoding overlaps with
/// <see cref="CffDictionaryReader"/> for the shared byte ranges, but the operator range is wider
/// (0-31 vs. 0-21) and marker byte 255 means a 16.16 fixed-point real here, not an unused/reserved
/// value as in a DICT.
/// </summary>
internal ref struct CffCharStringReader
{
    private readonly ReadOnlySpan<byte> _data;

    public CffCharStringReader(in ReadOnlySpan<byte> data)
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

        if (firstByte == CffConstants.OperandShortInt)
        {
            if (!TryReadByte(out byte highByte) || !TryReadByte(out byte lowByte))
            {
                return false;
            }

            var integer = (short)((highByte << 8) | lowByte);
            value = new CffValue(CffValueType.Integer, integer);
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

            int integer = ((firstByte - CffConstants.OperandPositiveIntStart) << 8)
                + nextByte
                + CffConstants.TwoByteIntegerBias;
            value = new CffValue(CffValueType.Integer, integer);
            return true;
        }

        if (firstByte >= CffConstants.OperandNegativeIntStart && firstByte <= CffConstants.OperandNegativeIntEnd)
        {
            if (!TryReadByte(out byte nextByte))
            {
                return false;
            }

            int integer = (-(firstByte - CffConstants.OperandNegativeIntStart) << 8)
                - nextByte
                - CffConstants.TwoByteIntegerBias;
            value = new CffValue(CffValueType.Integer, integer);
            return true;
        }

        if (firstByte == CffConstants.CharStringOperandFixed)
        {
            if (!TryReadByte(out byte firstOctet)
                || !TryReadByte(out byte secondOctet)
                || !TryReadByte(out byte thirdOctet)
                || !TryReadByte(out byte fourthOctet))
            {
                return false;
            }

            int fixedValue = (firstOctet << 24) | (secondOctet << 16) | (thirdOctet << 8) | fourthOctet;
            float realValue = fixedValue / 65536f;
            value = new CffValue(CffValueType.Real, Unsafe.As<float, int>(ref realValue));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Advances past the given number of raw bytes (used for hintmask/cntrmask mask data), without
    /// interpreting them.
    /// </summary>
    /// <returns>True if the bytes were available and skipped, false if not enough data remained.</returns>
    public bool TrySkipBytes(int count)
    {
        if (Position + count > _data.Length)
        {
            return false;
        }

        Position += count;
        return true;
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
