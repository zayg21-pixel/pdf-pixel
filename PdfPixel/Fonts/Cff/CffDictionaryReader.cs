using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// High-level reader for CFF DICT structures (both Top DICT and Private DICT).
/// Handles operand decoding and provides operator/operand pairs.
/// </summary>
internal ref struct CffDictionaryReader
{
    private const byte OperatorEscape = 12;

    private const byte OperandIntLow = 32;
    private const byte OperandIntHigh = 246;
    private const byte OperandPositiveIntStart = 247;
    private const byte OperandPositiveIntEnd = 250;
    private const byte OperandNegativeIntStart = 251;
    private const byte OperandNegativeIntEnd = 254;
    private const byte OperandShortInt = 28;
    private const byte OperandLongInt = 29;
    private const byte OperandRealNumber = 30;

    private readonly ReadOnlySpan<byte> _dictBytes;
    private int _position;
    private readonly List<decimal> _operandStack;
    private readonly ILogger _logger;

    public CffDictionaryReader(in ReadOnlySpan<byte> dictBytes, ILogger logger)
    {
        _dictBytes = dictBytes;
        _position = 0;
        _operandStack = new List<decimal>(capacity: 4);
        _logger = logger;
    }

    /// <summary>
    /// Attempts to read the next operator and its associated operands from the dictionary.
    /// Returns false when the end of the dictionary is reached or on parse error.
    /// </summary>
    /// <param name="operator">The operator byte (may be escaped, returns the second byte after escape).</param>
    /// <param name="operands">Array of operand values for this operator.</param>
    /// <returns>True if an operator was read successfully, false otherwise.</returns>
    public bool TryReadNextOperator(out byte @operator, out decimal[] operands)
    {
        @operator = 0;
        operands = Array.Empty<decimal>();

        _operandStack.Clear();

        while (_position < _dictBytes.Length)
        {
            byte currentByte = _dictBytes[_position++];

            if (currentByte == OperatorEscape)
            {
                if (_position >= _dictBytes.Length)
                {
                    return false;
                }

                @operator = _dictBytes[_position++];
                operands = _operandStack.ToArray();
                return true;
            }

            if (IsOperator(currentByte))
            {
                @operator = currentByte;
                operands = _operandStack.ToArray();
                return true;
            }

            // Reserved/unrecognized leading bytes can appear in malformed DICTs; skip them and
            // keep scanning so a later valid operator is still reached instead of aborting the DICT.
            if (TryReadOperand(currentByte, out decimal operandValue))
            {
                _operandStack.Add(operandValue);
            }
            else
            {
                _logger.LogWarning("Failed to parse CFF DICT operand starting with byte {ByteValue} at position {Position}; skipping it.", currentByte, _position - 1);
            }
        }

        return false;
    }

    private static bool IsOperator(byte b) => b <= 21 || b == OperatorEscape;

    private bool TryReadOperand(byte firstByte, out decimal value)
    {
        value = 0;

        if (firstByte >= OperandIntLow && firstByte <= OperandIntHigh)
        {
            value = firstByte - 139;
            return true;
        }

        if (firstByte >= OperandPositiveIntStart && firstByte <= OperandPositiveIntEnd)
        {
            if (_position >= _dictBytes.Length)
            {
                return false;
            }

            byte nextByte = _dictBytes[_position++];
            int intValue = ((firstByte - 247) * 256) + nextByte + 108;
            value = intValue;
            return true;
        }

        if (firstByte >= OperandNegativeIntStart && firstByte <= OperandNegativeIntEnd)
        {
            if (_position >= _dictBytes.Length)
            {
                return false;
            }

            byte nextByte = _dictBytes[_position++];
            int intValue = (-(firstByte - 251) * 256) - nextByte - 108;
            value = intValue;
            return true;
        }

        if (firstByte == OperandShortInt)
        {
            if (_position + 1 >= _dictBytes.Length)
            {
                return false;
            }

            var shortValue = (short)(_dictBytes[_position] << 8 | _dictBytes[_position + 1]);
            _position += 2;
            value = shortValue;
            return true;
        }

        if (firstByte == OperandLongInt)
        {
            if (_position + 3 >= _dictBytes.Length)
            {
                return false;
            }

            int intValue = _dictBytes[_position] << 24
                | _dictBytes[_position + 1] << 16
                | _dictBytes[_position + 2] << 8
                | _dictBytes[_position + 3];
            _position += 4;
            value = intValue;
            return true;
        }

        if (firstByte == OperandRealNumber)
        {
            return TryReadRealNumber(out value);
        }

        return false;
    }

    private bool TryReadRealNumber(out decimal value)
    {
        value = 0;

        char[] numberChars = ArrayPool<char>.Shared.Rent(64);
        int charCount = 0;

        try
        {
            var finished = false;
            while (!finished && _position < _dictBytes.Length)
            {
                byte nibblePair = _dictBytes[_position++];
                var highNibble = (byte)((nibblePair >> 4) & 0xF);
                var lowNibble = (byte)(nibblePair & 0xF);

                TryProcessNibble(highNibble, numberChars, ref charCount, out finished);

                if (finished)
                {
                    break;
                }

                TryProcessNibble(lowNibble, numberChars, ref charCount, out finished);
            }

            if (charCount > 0)
            {
                string numberString = new(numberChars, 0, charCount);

                if (!decimal.TryParse(numberString, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    _logger.LogWarning("CFF DICT real number \"{NumberString}\" at position {Position} is out of range for decimal; using 0.", numberString, _position);
                    value = 0;
                }

                return true;
            }

            if (finished)
            {
                value = 0;
                return true;
            }

            return false;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(numberChars);
        }
    }

    private static bool TryProcessNibble(byte nibble, char[] buffer, ref int charCount, out bool finished)
    {
        finished = false;

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
                    if (charCount >= buffer.Length)
                    {
                        return false;
                    }

                    buffer[charCount++] = (char)('0' + nibble);
                    return true;
                }
            case 0xA:
                {
                    if (charCount >= buffer.Length)
                    {
                        return false;
                    }

                    buffer[charCount++] = '.';
                    return true;
                }
            case 0xB:
                {
                    if (charCount + 1 >= buffer.Length)
                    {
                        return false;
                    }

                    buffer[charCount++] = 'E';
                    buffer[charCount++] = '+';
                    return true;
                }
            case 0xC:
                {
                    if (charCount + 1 >= buffer.Length)
                    {
                        return false;
                    }

                    buffer[charCount++] = 'E';
                    buffer[charCount++] = '-';
                    return true;
                }
            case 0xD:
                return false;

            case 0xE:
                {
                    if (charCount >= buffer.Length)
                    {
                        return false;
                    }

                    buffer[charCount++] = '-';
                    return true;
                }
            case 0xF:
                {
                    finished = true;
                    return true;
                }
            default:
                return false;
        }
    }
}
