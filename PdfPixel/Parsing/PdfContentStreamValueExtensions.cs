using PdfPixel.Objects;
using System.Runtime.CompilerServices;

namespace PdfPixel.Parsing;

/// <summary>
/// Resolves content stream tokens, parsing names, strings, arrays and dictionaries from the content stream they were read from.
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
                return PdfParser.ReadContentValue(context, value.Value, value.Length);
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
    /// Returns the operator an operator token holds.
    /// </summary>
    public static PdfOperator GetOperator(this in PdfContentStreamValue value) => (PdfOperator)value.Value;

    private static float GetReal(in PdfContentStreamValue value)
    {
        int bits = value.Value;

        return Unsafe.As<int, float>(ref bits);
    }
}
