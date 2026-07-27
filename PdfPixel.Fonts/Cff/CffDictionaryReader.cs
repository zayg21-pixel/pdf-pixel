using System;
using System.Globalization;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads CFF DICT data one value at a time.
/// </summary>
internal ref struct CffDictionaryReader
{
    private readonly ReadOnlySpan<byte> _dictBytes;
    private int _position;

    public CffDictionaryReader(in ReadOnlySpan<byte> dictBytes)
    {
        _dictBytes = dictBytes;
        _position = 0;
    }

    public readonly bool IsAtEnd => _position >= _dictBytes.Length;

    /// <summary>
    /// Reads the next value (operand or operator) from the DICT data.
    /// </summary>
    /// <returns>The next value, or null if the end of the data has been reached or the data is malformed.</returns>
    public ICffValue? ReadNextValue()
    {
        if (!TryReadByte(out byte firstByte))
        {
            return null;
        }

        if (firstByte == CffConstants.DictOperatorEscape)
        {
            if (!TryReadByte(out byte secondByte))
            {
                return null;
            }

            return new CffValue<byte>(secondByte, CffValueType.EscapedOperator);
        }

        if (firstByte <= CffConstants.DictOperatorMax)
        {
            return new CffValue<byte>(firstByte, CffValueType.Operator);
        }

        if (firstByte >= CffConstants.OperandIntLow && firstByte <= CffConstants.OperandIntHigh)
        {
            return new CffValue<int>(firstByte - CffConstants.SingleByteIntegerBias, CffValueType.Integer);
        }

        if (firstByte >= CffConstants.OperandPositiveIntStart && firstByte <= CffConstants.OperandPositiveIntEnd)
        {
            if (!TryReadByte(out byte nextByte))
            {
                return null;
            }

            int value = ((firstByte - CffConstants.OperandPositiveIntStart) << 8)
                + nextByte
                + CffConstants.TwoByteIntegerBias;
            return new CffValue<int>(value, CffValueType.Integer);
        }

        if (firstByte >= CffConstants.OperandNegativeIntStart && firstByte <= CffConstants.OperandNegativeIntEnd)
        {
            if (!TryReadByte(out byte nextByte))
            {
                return null;
            }

            int value = (-(firstByte - CffConstants.OperandNegativeIntStart) << 8)
                - nextByte
                - CffConstants.TwoByteIntegerBias;
            return new CffValue<int>(value, CffValueType.Integer);
        }

        if (firstByte == CffConstants.OperandShortInt)
        {
            if (!TryReadByte(out byte highByte) || !TryReadByte(out byte lowByte))
            {
                return null;
            }

            var value = (short)((highByte << 8) | lowByte);
            return new CffValue<int>(value, CffValueType.Integer);
        }

        if (firstByte == CffConstants.OperandLongInt)
        {
            if (!TryReadByte(out byte firstOctet)
                || !TryReadByte(out byte secondOctet)
                || !TryReadByte(out byte thirdOctet)
                || !TryReadByte(out byte fourthOctet))
            {
                return null;
            }

            int value = (firstOctet << 24) | (secondOctet << 16) | (thirdOctet << 8) | fourthOctet;
            return new CffValue<int>(value, CffValueType.Integer);
        }

        if (firstByte == CffConstants.OperandRealNumber)
        {
            return new CffValue<float>(ReadRealNumber(), CffValueType.Real);
        }

        return null;
    }

    private bool TryReadByte(out byte value)
    {
        if (IsAtEnd)
        {
            value = 0;
            return false;
        }

        value = _dictBytes[_position++];
        return true;
    }

    private float ReadRealNumber()
    {
        Span<char> buffer = stackalloc char[32];
        int length = 0;
        var finished = false;

        while (!finished && TryReadByte(out byte nibblePair))
        {
            finished = AppendNibble((byte)(nibblePair >> 4), buffer, ref length);
            if (!finished)
            {
                finished = AppendNibble((byte)(nibblePair & 0xF), buffer, ref length);
            }
        }

        if (length == 0)
        {
            return 0f;
        }

        string numberString = buffer.Slice(0, length).ToString();

        if (!float.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return 0f;
        }

        return value;
    }

    private static bool AppendNibble(byte nibble, in Span<char> buffer, ref int length)
    {
        switch (nibble)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
            case 0x8:
            case 0x9:
                {
                    buffer[length++] = (char)('0' + nibble);
                    return false;
                }
            case CffConstants.RealNibbleDecimalPoint:
                {
                    buffer[length++] = '.';
                    return false;
                }
            case CffConstants.RealNibblePositiveExponent:
                {
                    buffer[length++] = 'E';
                    buffer[length++] = '+';
                    return false;
                }
            case CffConstants.RealNibbleNegativeExponent:
                {
                    buffer[length++] = 'E';
                    buffer[length++] = '-';
                    return false;
                }
            case CffConstants.RealNibbleMinus:
                {
                    buffer[length++] = '-';
                    return false;
                }
            case CffConstants.RealNibbleReserved:
                return false;

            case CffConstants.RealNibbleTerminator:
            default:
                return true;
        }
    }
}
