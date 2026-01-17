using PdfRender.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfRender.Parsing;

partial struct PdfParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IPdfValue ReadNumber()
    {
        // Single-pass number parsing.
        // Accumulates all digits (integer and fractional) into one combined value.
        // Fractional digit count determines scaling using precomputed inverse powers of10.
        bool isNegative = false;
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
        bool afterDot = false;
        bool sawDigit = false;

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
                combined = combined * 10d + (current - Zero);

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
            return PdfValueFactory.Integer(0);
        }

        if (fractionalDigits == 0)
        {
            int intValue = (int)combined;
            if (isNegative)
            {
                intValue = -intValue;
            }
            return PdfValueFactory.Integer(intValue);
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

        double realValue = combined * scale;

        if (isNegative)
        {
            realValue = -realValue;
        }

        return PdfValueFactory.Real((float)realValue);
    }
}
