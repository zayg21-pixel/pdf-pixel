using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Extension methods for reading numeric values out of <see cref="CffValue"/> operands.
/// </summary>
internal static class CffValueExtensions
{
    /// <summary>
    /// Gets the value as an integer, widening a real operand if necessary.
    /// </summary>
    public static int AsInt(this in CffValue value)
    {
        switch (value.Type)
        {
            case CffValueType.Integer:
            {
                return value.Value;
            }
            case CffValueType.Real:
            {
                return (int)GetReal(value);
            }
            default:
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Gets the value as a float, widening an integer operand if necessary.
    /// </summary>
    public static float AsFloat(this in CffValue value)
    {
        switch (value.Type)
        {
            case CffValueType.Integer:
            {
                return value.Value;
            }
            case CffValueType.Real:
            {
                return GetReal(value);
            }
            default:
            {
                return 0f;
            }
        }
    }

    /// <summary>
    /// Gets the last operand as an integer, or null if the list is empty.
    /// </summary>
    public static int? LastAsInt(this IReadOnlyList<CffValue> operands) => (operands.Count > 0) ? operands[operands.Count - 1].AsInt() : null;

    /// <summary>
    /// Gets the last operand as a float, or null if the list is empty.
    /// </summary>
    public static float? LastAsFloat(this IReadOnlyList<CffValue> operands) => (operands.Count > 0) ? operands[operands.Count - 1].AsFloat() : null;

    /// <summary>
    /// Converts every operand in the list to a float array.
    /// </summary>
    public static float[] ToFloatArray(this IReadOnlyList<CffValue> operands)
    {
        var result = new float[operands.Count];
        for (int operandIndex = 0; operandIndex < operands.Count; operandIndex++)
        {
            result[operandIndex] = operands[operandIndex].AsFloat();
        }

        return result;
    }

    private static float GetReal(in CffValue value)
    {
        int bits = value.Value;

        return Unsafe.As<int, float>(ref bits);
    }
}
