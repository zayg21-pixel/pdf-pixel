using System;
using System.Collections.Generic;
using System.IO;
using PdfReader.Models;

namespace PdfReader.Fonts.PsFont
{
    public struct Type1ConverterContext
    {
        public Dictionary<int, byte[]> LocalSubrs { get; set; }

        public bool WasClosePath { get; set; }

        public bool InFlexSequence { get; set; }

        public List<int> FlexDeltas { get; set; }
    }

    /// <summary>
    /// Static helper class for shallow Type1 -> Type2 CharString conversion.
    /// NOTE: This is a minimal placeholder. TODO: Implement full semantics (width handling, hsbw/sbw distinction, flex expansion, subroutine bias, hint operators, seac composition, OtherSubrs, division, real numbers).
    /// Flex handling logic refactored; current implementation preserves flex sequences per provided refactor.
    /// </summary>
    internal static class Type1CharStringConverter
    {
        // Operator constants retained for future full conversion.
        private const byte OpHStem = 1; // hstem (hint) – discard operands in pairs
        private const byte OpVStem = 3; // vstem (hint) – discard operands in pairs
        private const byte OpHsbw = 13; // hsbw (Type1 only) – TODO: distinguish from sbw
        private const byte OpEndChar = 14;
        private const byte OpRMoveTo = 21;
        private const byte OpHMoveTo = 22;
        private const byte OpVMoveTo = 4;
        private const byte OpRLineTo = 5;
        private const byte OpHLineTo = 6;
        private const byte OpVLineTo = 7;
        private const byte OpRRCurveTo = 8;
        private const byte OpCallSubr = 10; // Subroutine call – one operand index
        private const byte OpReturn = 11; // Return from subroutine
        private const byte OpEscape = 12;
        private const byte OpClosePath = 9; // Type1 only (deprecated)
        private const byte OpRCurveLine = 24; // rcurveline
        private const byte OpRLineCurve = 25; // rlinecurve
        private const byte OpVVCurveTo = 26; // vvcurveto
        private const byte OpHHCurveTo = 27; // hhcurveto
        private const byte OpVHCurveTo = 30; // vhcurveto
        private const byte OpHVCurveTo = 31; // hvcurveto
        private const byte EscDiv = 12;
        private const byte EscSbw = 7;
        private const byte EscCallOtherSubr = 16; // callothersubr, the only handled escape operator (flex). seac currently unsupported (TODO remains in summary)

        /// <summary>
        /// Convert all Type1 charstrings and flatten local subroutines (inlines their content). Skips hints.
        /// </summary>
        public static Dictionary<PdfString, byte[]> ConvertAllCharStringsToType2Flatten(Dictionary<PdfString, byte[]> source, Type1ConverterContext context)
        {
            var result = new Dictionary<PdfString, byte[]>(source.Count);
            foreach (var kv in source)
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
            var operandStack = new List<int>(32);
            ProcessCharString(root, ref context, output, operandStack);

            return output.ToArray();
        }

        /// <summary>
        /// Recursive processor that inlines subroutines while preserving operand stack semantics.
        /// </summary>
        private static void ProcessCharString(byte[] data, ref Type1ConverterContext context, MemoryStream output, List<int> operandStack)
        {
            for (int i = 0; i < data.Length; i++)
            {
                int b = data[i];

                // Number decoding
                if (b >= 32 && b <= 246)
                {
                    operandStack.Add(b - 139);
                    continue;
                }
                if (b >= 247 && b <= 250)
                {
                    if (i + 1 >= data.Length)
                    {
                        break;
                    }
                    int b1 = data[++i];
                    operandStack.Add((b - 247) * 256 + b1 + 108);
                    continue;
                }
                if (b >= 251 && b <= 254)
                {
                    if (i + 1 >= data.Length)
                    {
                        break;
                    }
                    int b1 = data[++i];
                    operandStack.Add(-(b - 251) * 256 - b1 - 108);
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
                    operandStack.Add(value);
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

                // Hint operators – discard operands (pairs) & do not output
                if (b == OpHStem || b == OpVStem)
                {
                    operandStack.Clear();
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
                    int sbx = operandStack[operandStack.Count - 2];
                    // int wx = operandStack[operandStack.Count - 1]; // ignored for now
                    WriteNumber(output, sbx);
                    output.WriteByte(OpHMoveTo);
                    operandStack.Clear();
                    continue;
                }

                // Subroutine handling
                if (b == OpCallSubr)
                {
                    int subrIndex = operandStack.Count > 0 ? operandStack[operandStack.Count - 1] : -1;
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
                            foreach (int v in operandStack)
                            {
                                WriteNumber(output, v);
                            }

                            operandStack.Clear();
                            output.WriteByte((byte)b);
                        }

                        break;
                    }
                    case OpHMoveTo:
                    case OpVMoveTo:
                    case OpRLineTo:
                    case OpHLineTo:
                    case OpVLineTo:
                    case OpRRCurveTo:
                    case OpRCurveLine:
                    case OpRLineCurve:
                    case OpVVCurveTo:
                    case OpHHCurveTo:
                    case OpVHCurveTo:
                    case OpHVCurveTo:
                    {
                        foreach (int v in operandStack)
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

        private static void HandleEscapeSequence(ref Type1ConverterContext context, MemoryStream output, List<int> operandStack, byte esc)
        {
            switch (esc)
            {
                case EscDiv:
                {
                    foreach (int v in operandStack)
                    {
                        WriteNumber(output, v);
                    }

                    operandStack.Clear();

                    output.WriteByte(OpEscape);
                    output.WriteByte(EscDiv);
                    break;
                }
                case EscSbw:
                {
                    int sbx = operandStack[operandStack.Count - 4];
                    int sby = operandStack[operandStack.Count - 3];
                    // int swx = operandStack[operandStack.Count - 2]; next 2 operands are wx, wy – ignored in Type2 conversion, but we can use them for metrics
                    // int swy = operandStack[operandStack.Count - 1];
                    WriteNumber(output, sbx);
                    WriteNumber(output, sby);
                    output.WriteByte(OpRMoveTo);

                    operandStack.Clear();
                    break;
                }
                case EscCallOtherSubr:
                {
                    if (operandStack.Count == 0)
                    {
                        break;
                    }
                    var index = operandStack[operandStack.Count - 1];

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
                                    context.FlexDeltas = new List<int>();
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
                                foreach (int item in context.FlexDeltas)
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
                default:
                {
                    operandStack.Clear();
                    break;
                }
            }
        }

        /// <summary>
        /// Encode a single integer using Type2 (CFF) CharString number encoding.
        /// Uses compact 1- or 2-byte encodings when possible.
        /// Falls back to shortint (28 + 2 bytes) for values within 16-bit range
        /// outside of the quick ranges, and longint (255 + 4 bytes) otherwise.
        /// NOTE: Type1 does not have the 28 or 255 forms; this method intentionally emits Type2 encodings.
        /// </summary>
        private static void WriteNumber(Stream s, int value)
        {
            // 1-byte encoding: -107 .. 107
            if (value >= -107 && value <= 107)
            {
                s.WriteByte((byte)(value + 139));
                return;
            }

            // 2-byte positive: 108 .. 1131
            if (value >= 108 && value <= 1131)
            {
                int v = value - 108;
                byte b1 = (byte)(v / 256);
                byte b2 = (byte)(v % 256);
                s.WriteByte((byte)(247 + b1));
                s.WriteByte(b2);
                return;
            }

            // 2-byte negative: -1131 .. -108
            if (value >= -1131 && value <= -108)
            {
                int v = -value - 108;
                byte b1 = (byte)(v / 256);
                byte b2 = (byte)(v % 256);
                s.WriteByte((byte)(251 + b1));
                s.WriteByte(b2);
                return;
            }

            // ShortInt (Type2 only): -32768 .. 32767 excluding ranges already handled above.
            if (value >= -32768 && value <= 32767)
            {
                s.WriteByte(28); // shortint marker
                unchecked
                {
                    s.WriteByte((byte)((value >> 8) & 0xFF));
                    s.WriteByte((byte)(value & 0xFF));
                }
                return;
            }

            // LongInt (Type2 only): 255 + 4 bytes big-endian two's complement
            s.WriteByte(255);
            unchecked
            {
                s.WriteByte((byte)((value >> 24) & 0xFF));
                s.WriteByte((byte)((value >> 16) & 0xFF));
                s.WriteByte((byte)((value >> 8) & 0xFF));
                s.WriteByte((byte)(value & 0xFF));
            }
        }
    }
}
