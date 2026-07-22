using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Extension methods for reading numeric values out of <see cref="ICffValue"/> operands.
/// </summary>
internal static class ICffValueExtensions
{
    /// <summary>
    /// Gets the value as an integer, widening a real operand if necessary.
    /// </summary>
    public static int AsInt(this ICffValue value)
    {
        return value switch
        {
            CffValue<int> intValue => intValue.Value,
            CffValue<float> floatValue => (int)floatValue.Value,
            _ => 0
        };
    }

    /// <summary>
    /// Gets the value as a float, widening an integer operand if necessary.
    /// </summary>
    public static float AsFloat(this ICffValue value)
    {
        return value switch
        {
            CffValue<int> intValue => intValue.Value,
            CffValue<float> floatValue => floatValue.Value,
            _ => 0f
        };
    }

    /// <summary>
    /// Gets the last operand as an integer, or null if the list is empty.
    /// </summary>
    public static int? LastAsInt(this IReadOnlyList<ICffValue> operands) => (operands.Count > 0) ? operands[operands.Count - 1].AsInt() : null;

    /// <summary>
    /// Gets the last operand as a float, or null if the list is empty.
    /// </summary>
    public static float? LastAsFloat(this IReadOnlyList<ICffValue> operands) => (operands.Count > 0) ? operands[operands.Count - 1].AsFloat() : null;

    /// <summary>
    /// Converts every operand in the list to a float array.
    /// </summary>
    public static float[] ToFloatArray(this IReadOnlyList<ICffValue> operands)
    {
        var result = new float[operands.Count];
        for (int operandIndex = 0; operandIndex < operands.Count; operandIndex++)
        {
            result[operandIndex] = operands[operandIndex].AsFloat();
        }

        return result;
    }
}
