using System;
using System.Collections.Generic;
using System.IO;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts.Type1;

/// <summary>
/// Static helper class for shallow Type1 -> Type2 CharString conversion.
/// </summary>
internal static class Type1CharStringConverter
{
    // Operator constants retained for future full conversion.
    private const byte OpHStem = 1;
    private const byte OpVStem = 3;
    private const byte OpHsbw = 13;

    // path drawing/move operators
    private const byte OpVMoveTo = 4;
    private const byte OpRLineTo = 5;
    private const byte OpHLineTo = 6;
    private const byte OpVLineTo = 7;
    private const byte OpRRCurveTo = 8;
    private const byte OpRMoveTo = 21;
    private const byte OpHMoveTo = 22;
    private const byte OpVHCurveTo = 30;
    private const byte OpHVCurveTo = 31;

    private const byte OpCallSubr = 10;
    private const byte OpReturn = 11;
    private const byte OpEscape = 12;
    private const byte OpEndChar = 14;
    private const byte OpClosePath = 9; // Type1 only (deprecated)
    private const byte EscSbw = 7;
    private const byte EscSeac = 6;
    private const byte EscDiv = 12;
    private const byte EscCallOtherSubr = 16;

    /// <summary>
    /// Convert all Type1 charstrings and flatten local subroutines (inlines their content). Skips hints.
    /// </summary>
    public static Dictionary<PdfString, byte[]> ConvertAllCharStringsToType2Flatten(Type1ConverterContext context)
    {
        var result = new Dictionary<PdfString, byte[]>(context.Source.Count);
        foreach (var kv in context.Source)
        {
            var oldValue = kv.Value;
            var newValue = FlattenCharString(oldValue, context);
            result[kv.Key] = newValue;

        }
        return result;
    }

    private static byte[] FlattenCharString(byte[] root, Type1ConverterContext context)
    {
        if (root == null || root.Length == 0)
        {
            return Array.Empty<byte>();
        }
        var output = new MemoryStream(root.Length + 32);
        var operandStack = new List<Type1CharStringNumber>(32);
        ProcessCharString(root, ref context, output, operandStack);

        return output.ToArray();
    }

    /// <summary>
    /// Recursive processor that inlines subroutines while preserving operand stack semantics.
    /// </summary>
    private static void ProcessCharString(byte[] data, ref Type1ConverterContext context, MemoryStream output, List<Type1CharStringNumber> operandStack)
    {
        for (int i = 0; i < data.Length; i++)
        {
            int b = data[i];

            // Number decoding
            if (b >= 32 && b <= 246)
            {
                operandStack.Add(new Type1CharStringNumber(b - 139));
                continue;
            }
            if (b >= 247 && b <= 250)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                int b1 = data[++i];
                operandStack.Add(new Type1CharStringNumber((b - 247) * 256 + b1 + 108));
                continue;
            }
            if (b >= 251 && b <= 254)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                int b1 = data[++i];
                operandStack.Add(new Type1CharStringNumber(-(b - 251) * 256 - b1 - 108));
                continue;
            }
            if (b == 255)
            {
                if (i + 4 >= data.Length)
                {
                    break;
                }
                int value = (data[i + 1] << 24) | (data[i + 2] << 16) | (data[i + 3] << 8) | data[i + 4];
                i += 4;
                operandStack.Add(new Type1CharStringNumber(value));
                continue;
            }

            // Escaped operators
            if (b == OpEscape)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                byte esc = data[++i];
                HandleEscapeSequence(ref context, output, operandStack, esc);
                continue;
            }

            // ClosePath (Type1 only) – ignore
            if (b == OpClosePath)
            {
                operandStack.Clear();
                continue;
            }

            // hsbw: first operator; sets sidebearing and width (sbx, wx). Convert to width + hmoveto sequence.
            if (b == OpHsbw)
            {
                var sbx = operandStack[operandStack.Count - 2];
                var wx = operandStack[operandStack.Count - 1];
                WriteNumber(output, sbx);
                UpdateCoordinates(ref context, OpHMoveTo, new List<Type1CharStringNumber> { sbx });
                output.WriteByte(OpHMoveTo);

                context.SideBearingX = sbx.GetAsDouble();
                context.WidthX = wx.GetAsDouble();

                operandStack.Clear();
                continue;
            }

            // Subroutine handling
            if (b == OpCallSubr)
            {
                int subrIndex = operandStack.Count > 0 ? operandStack[operandStack.Count - 1].Value1 : -1;
                if (operandStack.Count > 0)
                {
                    operandStack.RemoveAt(operandStack.Count - 1);
                }
                if (subrIndex >= 0 && context.LocalSubrs != null && context.LocalSubrs.TryGetValue(subrIndex, out byte[] subrBytes))
                {
                    ProcessCharString(subrBytes, ref context, output, operandStack);
                }
                continue;
            }

            if (b == OpReturn)
            {
                // End current subroutine – leave remaining operands for caller
                return;
            }
            if (b == OpEndChar)
            {
                if (context.SkipEndChar)
                {
                    return;
                }

                output.WriteByte(OpEndChar);
                return;
            }

            switch (b)
            {
                case OpRMoveTo:
                {
                    // rmoveto operator inside flex sequence is skipped
                    if (context.InFlexSequence)
                    {
                        // we're collecting points in escape sequence
                        continue;
                    }
                    else
                    {
                        UpdateCoordinates(ref context, b, operandStack);

                        foreach (var v in operandStack)
                        {
                            WriteNumber(output, v);
                        }

                        operandStack.Clear();
                        output.WriteByte((byte)b);
                    }

                    break;
                }
                case OpHStem:
                case OpVStem:
                case OpHMoveTo:
                case OpVMoveTo:
                case OpRLineTo:
                case OpHLineTo:
                case OpVLineTo:
                case OpRRCurveTo:
                case OpVHCurveTo:
                case OpHVCurveTo:
                {

                    UpdateCoordinates(ref context, b, operandStack);

                    foreach (var v in operandStack)
                    {
                        WriteNumber(output, v);
                    }

                    operandStack.Clear();
                    output.WriteByte((byte)b);
                    break;
                }
                default:
                {
                    // Unknown operator – discard accumulated operands to avoid leakage.
                    operandStack.Clear();
                    break;
                }
            }
        }
    }

    private static void HandleEscapeSequence(ref Type1ConverterContext context, MemoryStream output, List<Type1CharStringNumber> operandStack, byte esc)
    {
        switch (esc)
        {
            case EscDiv:
            {
                var v1 = operandStack[operandStack.Count - 2];
                var v2 = operandStack[operandStack.Count - 1];

                operandStack.RemoveAt(operandStack.Count - 1);
                operandStack.RemoveAt(operandStack.Count - 1);
                
                v1.SetSecondValue(v2.Value1, ValueOperation.Div);
                operandStack.Add(v1);

                break;
            }
            case EscSbw:
            {
                var sbx = operandStack[operandStack.Count - 4];
                var sby = operandStack[operandStack.Count - 3];
                var swx = operandStack[operandStack.Count - 2];
                var swy = operandStack[operandStack.Count - 1];

                context.SideBearingX = sbx.GetAsDouble();
                context.SideBearingY = sby.GetAsDouble();
                context.WidthX = swx.GetAsDouble();
                context.WidthY = swy.GetAsDouble();

                WriteNumber(output, sbx);
                WriteNumber(output, sby);

                UpdateCoordinates(ref context, OpRMoveTo, new List<Type1CharStringNumber> { sbx, sby });
                output.WriteByte(OpRMoveTo);

                operandStack.Clear();
                break;
            }
            case EscSeac:
            {
                // asb adx ady bchar achar seac
                // a - accent, b - base character
                var asb = operandStack[operandStack.Count - 5];
                var adx = operandStack[operandStack.Count - 4];
                var ady = operandStack[operandStack.Count - 3];
                var bchar = operandStack[operandStack.Count - 2];
                var achar = operandStack[operandStack.Count - 1];
                operandStack.Clear();

                var standardEncoding = SingleByteEncodings.GetEncodingSet(Types.PdfFontEncoding.StandardEncoding);

                var nameA = standardEncoding[achar.Value1];
                var nameB = standardEncoding[bchar.Value1];
                var aBytes = context.Source[nameA];
                var bBytes = context.Source[nameB];

                // Base character first
                context.SkipEndChar = true;
                ProcessCharString(bBytes, ref context, output, operandStack);
                context.SkipEndChar = false;

                // Move to default position for accent
                double x = -context.X + context.SideBearingX + adx.GetAsDouble() - asb.GetAsDouble();
                double y = -context.Y + context.SideBearingY + ady.GetAsDouble();
                var type1X = Type1CharStringNumber.FromDouble(x);
                var type1Y = Type1CharStringNumber.FromDouble(y);
                WriteNumber(output, type1X);
                WriteNumber(output, type1Y);
                UpdateCoordinates(ref context, OpRMoveTo, new List<Type1CharStringNumber> { type1X, type1Y });
                output.WriteByte(OpRMoveTo);

                ProcessCharString(aBytes, ref context, output, operandStack);

                break;
            }
            case EscCallOtherSubr:
            {
                if (operandStack.Count == 0)
                {
                    break;
                }
                var index = operandStack[operandStack.Count - 1].Value1;

                switch (index)
                {
                    case 1:
                        context.InFlexSequence = true;
                        operandStack.Clear();
                        break;
                    case 2:
                    {
                        // accumulate flex deltas from rmoveto operands
                        for (int d = 0; d < operandStack.Count - 2; d++)
                        {
                            if (context.FlexDeltas == null)
                            {
                                context.FlexDeltas = new List<Type1CharStringNumber>();
                            }

                            context.FlexDeltas.Add(operandStack[d]);
                        }

                        operandStack.Clear();
                        break;
                    }
                    case 0:
                    {
                        if (context.FlexDeltas != null)
                        {
                            UpdateCoordinates(ref context, OpRRCurveTo, context.FlexDeltas);

                            foreach (var item in context.FlexDeltas)
                            {
                                WriteNumber(output, item);
                            }

                            output.WriteByte(OpRRCurveTo);
                        }
                        context.FlexDeltas = null;

                        context.InFlexSequence = false;
                        operandStack.Clear();
                        break;
                    }
                    default:
                        // unknown other subr – skip
                        operandStack.Clear();
                        break;
                }
                break;
            }
            case 1 or 2:
            {
                operandStack.Clear();
                // stem hinting, not supported, can be skipped
                break;
            }
            case 17 or 33:
            {
                operandStack.Clear();
                // additional flex operators, not needed as we use different flex logic
                break;
            }
            default:
            {
                operandStack.Clear();
                break;
            }
        }
    }

    private static void UpdateCoordinates(ref Type1ConverterContext context, int code, List<Type1CharStringNumber> operandStack)
    {
        switch (code)
        {
            case OpHMoveTo:
            case OpHLineTo:
                foreach (Type1CharStringNumber value in operandStack)
                {
                    context.X += value.GetAsDouble();
                }
                break;
            case OpVMoveTo:
            case OpVLineTo:
                foreach (Type1CharStringNumber value in operandStack)
                {
                    context.Y += value.GetAsDouble();
                }
                break;
            case OpRMoveTo:
            case OpRLineTo:
            case OpRRCurveTo:
                for (int i = 0; i < operandStack.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        context.X += operandStack[i].GetAsDouble();
                    }
                    else
                    {
                        context.Y += operandStack[i].GetAsDouble();
                    }
                }
                break;
            case OpVHCurveTo:
                for (int i = 0; i < operandStack.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        context.Y += operandStack[i].GetAsDouble();
                    }
                    else
                    {
                        context.X += operandStack[i].GetAsDouble();
                    }
                }
                break;
            case OpHVCurveTo:
                for (int i = 0; i < operandStack.Count; i++)
                {
                    if ((i / 2) % 2 == 0)
                    {
                        context.X += operandStack[i].GetAsDouble();
                    }
                    else
                    {
                        context.Y += operandStack[i].GetAsDouble();
                    }
                }
                break;
        }
    }

    private static void WriteNumber(Stream stream, Type1CharStringNumber value)
    {
        FontNumberConverter.EncodeCharStringNumber(stream, value.Value1);
        if (value.HasSecondValue)
        {
            if (value.Operation == ValueOperation.Div)
            {
                // Division requires special handling – emit div operator after the two numbers.
                FontNumberConverter.EncodeCharStringNumber(stream, value.Value2);
                stream.WriteByte(OpEscape);
                stream.WriteByte(EscDiv);
                return;
            }
            else
            {
                throw new NotSupportedException("Unsupported second value operation in Type1 to Type2 CharString conversion.");
            }
        }
    }
}
