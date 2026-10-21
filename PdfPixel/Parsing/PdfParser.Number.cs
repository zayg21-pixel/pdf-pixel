using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfValue ReadNumber()
    {
        if (ReadRawNumber(out int integerValue, out float realValue))
        {
            return PdfIntegerValue.FromInteger(integerValue);
        }

        return new PdfRealValue(realValue);
    }

    /// <summary>
    /// Reads a number token.
    /// </summary>
    /// <param name="integerValue">The number when it is an integer.</param>
    /// <param name="realValue">The number when it is a real.</param>
    /// <returns>True when the number is an integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadRawNumber(out int integerValue, out float realValue)
    {
        integerValue = 0;
        realValue = 0f;

        // Single-pass number parsing.
        // Accumulates all digits (integer and fractional) into one combined value.
        // Fractional digit count determines scaling using precomputed inverse powers of10.
        var isNegative = false;
        byte first = PeekByte();
        if (first == Minus)
        {
            isNegative = true;
            Advance(1);
        }
        else if (first == Plus)
        {
            Advance(1);
        }

        double combined = 0d;
        int fractionalDigits = 0;
        var afterDot = false;
        var sawDigit = false;

        while (!IsAtEnd)
        {
            byte current = PeekByte();

            if (!afterDot && current == Dot)
            {
                afterDot = true;
                Advance(1);
                continue;
            }

            if (current >= Zero && current <= Nine)
            {
                combined = (combined * 10d) + (current - Zero);

                if (afterDot)
                {
                    fractionalDigits++;
                }

                sawDigit = true;
                Advance(1);
                continue;
            }

            break;
        }

        if (!sawDigit)
        {
            // Malformed number like '+' '-' '.' -> treat as 0.
            return true;
        }

        if (fractionalDigits == 0)
        {
            var intValue = (int)combined;
            if (isNegative)
            {
                intValue = -intValue;
            }

            integerValue = intValue;
            return true;
        }

        // Scale once using lookup (fallback to Math.Pow for lengths beyond precomputed span).
        double scale;
        if (fractionalDigits < inversePowersOf10.Length)
        {
            scale = inversePowersOf10[fractionalDigits];
        }
        else
        {
            scale = Math.Pow(0.1d, fractionalDigits);
        }

        double scaledValue = combined * scale;

        if (isNegative)
        {
            scaledValue = -scaledValue;
        }

        realValue = (float)scaledValue;
        return false;
    }
}
