using PdfPixel.Geometry;
using PdfPixel.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves content stream tokens, against the content stream they were read from when held in its values.
/// </summary>
internal static class PdfContentStreamValueExtensions
{
    /// <summary>
    /// Converts the token into a <see cref="PdfValue"/>.
    /// </summary>
    /// <param name="value">The token.</param>
    /// <param name="context">The content stream the token was read from.</param>
    /// <returns>The value, or null when the token holds none.</returns>
    public static PdfValue? GetPdfValue(this in PdfContentStreamValue value, PdfParseContext context)
    {
        switch (value.Kind)
        {
            case PdfContentStreamValueKind.Integer:
            {
                return PdfIntegerValue.FromInteger(value.Value);
            }
            case PdfContentStreamValueKind.Real:
            {
                return new PdfRealValue(GetReal(value));
            }
            case PdfContentStreamValueKind.Boolean:
            {
                return (value.Value != 0) ? PdfBooleanValue.True : PdfBooleanValue.False;
            }
            case PdfContentStreamValueKind.Null:
            {
                return PdfNullValue.Instance;
            }
            case PdfContentStreamValueKind.Operator:
            {
                return PdfOperatorValue.FromOperator((PdfOperator)value.Value);
            }
            case PdfContentStreamValueKind.Value:
            {
                return context.Values[value.Value];
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Returns the token as a float if it is a number; otherwise returns null.
    /// </summary>
    public static float? AsFloat(this in PdfContentStreamValue value)
    {
        switch (value.Kind)
        {
            case PdfContentStreamValueKind.Integer:
            {
                return value.Value;
            }
            case PdfContentStreamValueKind.Real:
            {
                return GetReal(value);
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Returns the token as an integer if it is a number, truncating a real number; otherwise returns null.
    /// </summary>
    public static int? AsInteger(this in PdfContentStreamValue value)
    {
        switch (value.Kind)
        {
            case PdfContentStreamValueKind.Integer:
            {
                return value.Value;
            }
            case PdfContentStreamValueKind.Real:
            {
                return (int)GetReal(value);
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Returns the matrix [a b c d e f] given by the first six tokens if they are all numbers; otherwise returns null.
    /// </summary>
    public static PdfMatrix? AsMatrix(this in ReadOnlySpan<PdfContentStreamValue> operands) // TODO: move back to where it belongs
    {
        if (operands.Length < 6)
        {
            return null;
        }

        float? a = operands[0].AsFloat();
        float? b = operands[1].AsFloat();
        float? c = operands[2].AsFloat();
        float? d = operands[3].AsFloat();
        float? e = operands[4].AsFloat();
        float? f = operands[5].AsFloat();

        if (a == null
            || b == null
            || c == null
            || d == null
            || e == null
            || f == null)
        {
            return null;
        }

        return new PdfMatrix(a.Value, c.Value, e.Value, b.Value, d.Value, f.Value);
    }

    /// <summary>
    /// Returns the operator an operator token holds.
    /// </summary>
    public static PdfOperator GetOperator(this in PdfContentStreamValue value) => (PdfOperator)value.Value;

    private static float GetReal(in PdfContentStreamValue value)
    {
        int bits = value.Value;

        return Unsafe.As<int, float>(ref bits);
    }
}
