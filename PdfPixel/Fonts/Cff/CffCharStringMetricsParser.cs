using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Parses Type2 (CFF) charstrings to extract character metrics (width and sidebearings).
/// Does not execute the full charstring, only reads the initial width and first moveto for metrics.
/// </summary>
internal sealed class CffCharStringMetricsParser
{
    private const byte OpEscape = 12;
    private const byte OpShortInt = 28;
    private const byte OpHstem = 1;
    private const byte OpVstem = 3;
    private const byte OpVmoveto = 4;
    private const byte OpHstemhm = 18;
    private const byte OpHintmask = 19;
    private const byte OpCntrmask = 20;
    private const byte OpRmoveto = 21;
    private const byte OpHmoveto = 22;
    private const byte OpVstemhm = 23;
    private const byte OpEndchar = 14;

    private const byte EscDiv = 12;
    private const byte EscAdd = 10;
    private const byte EscSub = 11;
    private const byte EscMul = 24;

    /// <summary>
    /// Parses all charstrings from the CharStrings INDEX and extracts metrics for each glyph.
    /// </summary>
    /// <param name="cffData">Complete CFF data.</param>
    /// <param name="charStringsOffset">Offset to CharStrings INDEX.</param>
    /// <param name="glyphCount">Number of glyphs.</param>
    /// <param name="metrics">Array of character metrics indexed by GID.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParseCharStringMetrics(ReadOnlySpan<byte> cffData, int charStringsOffset, int glyphCount, out CffCharacterMetrics[] metrics)
    {
        metrics = new CffCharacterMetrics[glyphCount];

        var reader = new CffDataReader(cffData)
        {
            Position = charStringsOffset
        };

        if (!CffIndexReader.TryReadIndex(ref reader, out int count, out int dataStart, out int[] offsets, out _))
        {
            return false;
        }

        if (count != glyphCount)
        {
            return false;
        }

        for (ushort gid = 0; gid < count; gid++)
        {
            int charStringStart = dataStart + (offsets[gid] - 1);
            int charStringEnd = dataStart + (offsets[gid + 1] - 1);

            if (charStringStart < 0 || charStringEnd > cffData.Length || charStringEnd < charStringStart)
            {
                metrics[gid] = new CffCharacterMetrics();
                continue;
            }

            var charStringData = cffData.Slice(charStringStart, charStringEnd - charStringStart);
            metrics[gid] = ExtractMetrics(charStringData);
        }

        return true;
    }

    private static CffCharacterMetrics ExtractMetrics(ReadOnlySpan<byte> charStringData)
    {
        var operands = new Stack<decimal>();
        int position = 0;

        while (position < charStringData.Length)
        {
            if (TryReadNumber(charStringData, ref position, out decimal number))
            {
                operands.Push(number);
                continue;
            }

            byte b = charStringData[position];

            if (IsOperator(b))
            {
                if (b == OpEscape)
                {
                    if (position + 1 >= charStringData.Length)
                    {
                        break;
                    }

                    byte escOp = charStringData[position + 1];
                    position += 2;

                    if (!HandleEscapeOperator(escOp, operands))
                    {
                        operands.Clear();
                    }

                    continue;
                }

                return ExtractMetricsFromOperator(b, operands);
            }

            break;
        }

        return new CffCharacterMetrics();
    }

    private static bool HandleEscapeOperator(byte escOp, Stack<decimal> operands)
    {
        switch (escOp)
        {
            case EscDiv:
            {
                if (operands.Count < 2)
                {
                    return false;
                }

                decimal v2 = operands.Pop();
                decimal v1 = operands.Pop();

                if (v2 != 0)
                {
                    operands.Push(v1 / v2);
                }
                else
                {
                    operands.Push(0);
                }

                return true;
            }
            case EscAdd:
            {
                if (operands.Count < 2)
                {
                    return false;
                }

                decimal v2 = operands.Pop();
                decimal v1 = operands.Pop();
                operands.Push(v1 + v2);
                return true;
            }
            case EscSub:
            {
                if (operands.Count < 2)
                {
                    return false;
                }

                decimal v2 = operands.Pop();
                decimal v1 = operands.Pop();
                operands.Push(v1 - v2);
                return true;
            }
            case EscMul:
            {
                if (operands.Count < 2)
                {
                    return false;
                }

                decimal v2 = operands.Pop();
                decimal v1 = operands.Pop();
                operands.Push(v1 * v2);
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    private static CffCharacterMetrics ExtractMetricsFromOperator(byte op, Stack<decimal> operands)
    {
        var metrics = new CffCharacterMetrics();
        decimal[] operandArray = operands.ToArray();
        Array.Reverse(operandArray);
        int widthOffset = 0;

        switch (op)
        {
            case OpHstem:
            case OpVstem:
            case OpHstemhm:
            case OpVstemhm:
                if (operandArray.Length % 2 == 1)
                {
                    metrics.Width = (double)operandArray[0];
                }
                break;

            case OpHintmask:
            case OpCntrmask:
                if (operandArray.Length > 0)
                {
                    metrics.Width = (double)operandArray[0];
                }
                break;

            case OpHmoveto:
                if (operandArray.Length == 2)
                {
                    metrics.Width = (double)operandArray[0];
                    widthOffset = 1;
                }
                if (operandArray.Length > widthOffset)
                {
                    metrics.LeftSideBearing = (double)operandArray[widthOffset];
                }
                break;

            case OpVmoveto:
                if (operandArray.Length == 2)
                {
                    metrics.Width = (double)operandArray[0];
                }
                metrics.LeftSideBearing = 0;
                break;

            case OpRmoveto:
                if (operandArray.Length == 3)
                {
                    metrics.Width = (double)operandArray[0];
                    widthOffset = 1;
                }
                if (operandArray.Length > widthOffset)
                {
                    metrics.LeftSideBearing = (double)operandArray[widthOffset];
                }
                break;

            case OpEndchar:
                if (operandArray.Length > 0)
                {
                    metrics.Width = (double)operandArray[0];
                }
                break;
        }

        return metrics;
    }

    private static bool IsOperator(byte b)
    {
        return b < 32;
    }

    private static bool TryReadNumber(ReadOnlySpan<byte> data, ref int position, out decimal number)
    {
        number = 0;

        if (position >= data.Length)
        {
            return false;
        }

        byte b = data[position];

        if (b >= 32 && b <= 246)
        {
            number = b - 139;
            position++;
            return true;
        }

        if (b >= 247 && b <= 250)
        {
            if (position + 1 >= data.Length)
            {
                return false;
            }

            byte b1 = data[position + 1];
            number = (b - 247) * 256 + b1 + 108;
            position += 2;
            return true;
        }

        if (b >= 251 && b <= 254)
        {
            if (position + 1 >= data.Length)
            {
                return false;
            }

            byte b1 = data[position + 1];
            number = -(b - 251) * 256 - b1 - 108;
            position += 2;
            return true;
        }

        if (b == OpShortInt)
        {
            if (position + 2 >= data.Length)
            {
                return false;
            }

            short shortVal = (short)((data[position + 1] << 8) | data[position + 2]);
            number = shortVal;
            position += 3;
            return true;
        }

        if (b == 255)
        {
            if (position + 4 >= data.Length)
            {
                return false;
            }

            int fixedPoint = (data[position + 1] << 24) | (data[position + 2] << 16) | (data[position + 3] << 8) | data[position + 4];
            number = fixedPoint / 65536.0m;
            position += 5;
            return true;
        }

        return false;
    }
}
