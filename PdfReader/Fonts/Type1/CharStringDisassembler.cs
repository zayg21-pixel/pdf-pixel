using System;
using System.Collections.Generic;
using System.Text;

namespace PdfReader.Fonts.Type1;

/// <summary>
/// Utility class that converts Type1 / Type2 (CFF) charstring byte sequences into a human readable textual representation.
/// It expands operators to their mnemonic names and emits decoded integer operands in execution order.
/// NOTE: This is a diagnostic helper; it does not attempt deep semantic grouping (width handling, hints consumption, flex semantics, etc.).
/// </summary>
internal static class CharStringDisassembler
{
    private const byte OpEscape = 12;
    private const byte OpShortInt = 28; // Type2 only

    private static readonly Dictionary<int, string> Type1Operators = new Dictionary<int, string>
     {
         {1, "hstem" },
         {3, "vstem" },
         {4, "vmoveto" },
         {5, "rlineto" },
         {6, "hlineto" },
         {7, "vlineto" },
         {8, "rrcurveto" },
         {9, "closepath" },
         {10, "callsubr" },
         {11, "return" },
         {13, "hsbw" },
         {14, "endchar" },
         {21, "rmoveto" },
         {22, "hmoveto" },
         {24, "rcurveline" },
         {25, "rlinecurve" },
         {26, "vvcurveto" },
         {27, "hhcurveto" },
         {30, "vhcurveto" },
         {31, "hvcurveto" }
     };

    private static readonly Dictionary<int, string> Type1EscapeOperators = new Dictionary<int, string>
     {
         {0, "dotsection" },
         {1, "vstem3" },
         {2, "hstem3" },
         {6, "seac" },
         {7, "sbw" },
         {12, "div" },
         {16, "callothersubr" },
         {17, "pop" },
         {33, "setcurrentpoint" }
     };

    private static readonly Dictionary<int, string> Type2Operators = new Dictionary<int, string>
     {
         // Base set (shared codes with Type1) + Type2 only codes.
         {1, "hstem" },
         {3, "vstem" },
         {4, "vmoveto" },
         {5, "rlineto" },
         {6, "hlineto" },
         {7, "vlineto" },
         {8, "rrcurveto" },
         {10, "callsubr" },
         {11, "return" },
         {14, "endchar" },
         {18, "hstemhm" },
         {19, "hintmask" },
         {20, "cntrmask" },
         {21, "rmoveto" },
         {22, "hmoveto" },
         {23, "vstemhm" },
         {24, "rcurveline" },
         {25, "rlinecurve" },
         {26, "vvcurveto" },
         {27, "hhcurveto" },
         {29, "callgsubr" },
         {30, "vhcurveto" },
         {31, "hvcurveto" }
     };

    private static readonly Dictionary<int, string> Type2EscapeOperators = new Dictionary<int, string>
     {
         {34, "hflex" },
         {35, "flex" },
         {36, "hflex1" },
         {37, "flex1" }
     };

    /// <summary>
    /// Disassembles a raw Type1 or Type2 (CFF) charstring into a human readable sequence.
    /// </summary>
    /// <param name="data">The raw charstring bytes.</param>
    /// <param name="isType2">Set to true to use Type2 (CFF) operator set; otherwise Type1.</param>
    /// <returns>A string containing numbers and operator mnemonics separated by spaces.</returns>
    public static string Disassemble(byte[] data, bool isType2)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var sb = new StringBuilder(data.Length * 3);
        var operands = new List<int>();
        int i = 0;

        while (i < data.Length)
        {
            int b = data[i];

            // Number encoding (Type1 & Type2 common quick encodings)
            if (b >= 32 && b <= 246)
            {
                operands.Add(b - 139);
                i++;
                continue;
            }
            if (b >= 247 && b <= 250)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                int b1 = data[i + 1];
                operands.Add((b - 247) * 256 + b1 + 108);
                i += 2;
                continue;
            }
            if (b >= 251 && b <= 254)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                int b1 = data[i + 1];
                operands.Add(-(b - 251) * 256 - b1 - 108);
                i += 2;
                continue;
            }
            if (b == 255)
            {
                if (i + 4 >= data.Length)
                {
                    break;
                }
                int value = (data[i + 1] << 24) | (data[i + 2] << 16) | (data[i + 3] << 8) | data[i + 4];
                operands.Add(value);
                i += 5;
                continue;
            }
            if (isType2 && b == OpShortInt)
            {
                if (i + 2 >= data.Length)
                {
                    break;
                }
                short shortVal = (short)((data[i + 1] << 8) | data[i + 2]);
                operands.Add(shortVal);
                i += 3;
                continue;
            }

            // Escape operators
            if (b == OpEscape)
            {
                if (i + 1 >= data.Length)
                {
                    break;
                }
                int esc = data[i + 1];
                i += 2;
                FlushOperands(sb, operands);
                string name = isType2 ? Lookup(Type2EscapeOperators, esc) : Lookup(Type1EscapeOperators, esc);
                sb.Append(name);
                sb.Append(' ');
                continue;
            }

            // Regular operators
            FlushOperands(sb, operands);
            string opName = isType2 ? Lookup(Type2Operators, b) : Lookup(Type1Operators, b);
            sb.Append(opName);
            sb.Append(' ');
            i++;

            // End charstring early on endchar (diagnostic convenience)
            if (b == 14) // endchar
            {
                //break;
            }
        }

        FlushOperands(sb, operands); // trailing numbers without operator (should not happen normally)
        return sb.ToString().TrimEnd();
    }

    private static void FlushOperands(StringBuilder sb, List<int> operands)
    {
        if (operands.Count == 0)
        {
            return;
        }
        for (int index = 0; index < operands.Count; index++)
        {
            sb.Append(operands[index]);
            sb.Append(' ');
        }
        operands.Clear();
    }

    private static string Lookup(Dictionary<int, string> map, int code)
    {
        if (map.TryGetValue(code, out string name))
        {
            return name;
        }
        return "op" + code; // Fallback for unknown operator codes
    }
}
