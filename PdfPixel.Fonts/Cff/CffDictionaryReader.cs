using System;
using System.Globalization;
using System.Runtime.CompilerServices;

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

        if (firstByte <= CffConstants.DictOperatorMax)
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

        if (firstByte == CffConstants.OperandLongInt)
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

        if (firstByte == CffConstants.OperandRealNumber)
        {
            float realValue = ReadRealNumber();
            value = new CffValue(CffValueType.Real, Unsafe.As<float, int>(ref realValue));
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
