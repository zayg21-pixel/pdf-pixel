using PdfReader.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    partial struct PdfParser
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDigit(byte value)
        {
            return value >= Zero && value <= Nine;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPdfValue ReadNumber()
        {
            // Parse sign
            bool isNegative = false;
            
            byte firstByte = _parseContext.PeekByte();
            if (firstByte == Minus)
            {
                isNegative = true;
                _parseContext.Advance(1);
            }
            else if (firstByte == Plus)
            {
                _parseContext.Advance(1);
            }

            // Parse integer part
            long integerPart = 0;
            bool hasDigits = false;
            while (!_parseContext.IsAtEnd)
            {
                byte currentByte = _parseContext.PeekByte();
                if (!IsDigit(currentByte))
                {
                    break;
                }
                hasDigits = true;
                integerPart = integerPart * 10 + (_parseContext.ReadByte() - Zero);
            }

            // Parse fractional part if dot is present
            long fractionalPart = 0;
            int fractionalDigits = 0;
            
            if (!_parseContext.IsAtEnd && _parseContext.PeekByte() == Dot)
            {
                _parseContext.Advance(1); // Consume the dot
                while (!_parseContext.IsAtEnd)
                {
                    byte currentByte = _parseContext.PeekByte();
                    if (!IsDigit(currentByte))
                    {
                        break;
                    }
                    hasDigits = true; // Numbers like ".5" are valid
                    fractionalPart = fractionalPart * 10 + (_parseContext.ReadByte() - Zero);
                    fractionalDigits++;
                }
            }

            if (!hasDigits)
            {
                // Malformed number (like just "+" or "-"), return integer 0
                return PdfValue.Integer(0);
            }

            if (fractionalDigits > 0)
            {
                // Return as real (float) - handles cases like ".5", "123.45", "0.25"
                float value = integerPart + fractionalPart / MathF.Pow(10, fractionalDigits);
                if (isNegative)
                {
                    value = -value;
                }
                return PdfValue.Real(value);
            }
            else
            {
                // Return as integer - handles cases like "123", "-45", "0"
                int value = (int)integerPart;
                if (isNegative)
                {
                    value = -value;
                }
                return PdfValue.Integer(value);
            }
        }
    }
}
