using System;
using System.Collections.Generic;

namespace PdfPixel.Objects;

/// <summary>
/// Represents a content stream operator.
/// </summary>
public sealed class PdfOperatorValue : PdfValue
{
    private static readonly Dictionary<PdfOperator, PdfOperatorValue> SharedValues = CreateSharedValues();

    private PdfOperatorValue(PdfOperator value) => Value = value;

    /// <summary>
    /// Gets the operator.
    /// </summary>
    public PdfOperator Value { get; }

    /// <summary>
    /// Unpacks the token bytes of the operator code; <see cref="PdfOperator.Unknown"/> yields its name.
    /// </summary>
    internal string GetOperatorText()
    {
        var characters = new char[sizeof(uint)];
        int length = 0;

        for (var code = (uint)Value; code != 0; code >>= 8)
        {
            characters[length] = (char)(code & 0xFF);
            length++;
        }

        if (length == 0)
        {
            return Value.ToString();
        }

        return new string(characters, 0, length);
    }

    /// <summary>
    /// Returns the shared instance for a defined operator, or a new instance for any other operator code.
    /// </summary>
    /// <param name="value">The operator.</param>
    internal static PdfOperatorValue FromOperator(PdfOperator value)
    {
        if (SharedValues.TryGetValue(value, out PdfOperatorValue? sharedValue))
        {
            return sharedValue;
        }

        return new PdfOperatorValue(value);
    }

    private static Dictionary<PdfOperator, PdfOperatorValue> CreateSharedValues()
    {
        Dictionary<PdfOperator, PdfOperatorValue> sharedValues = [];

#if NETSTANDARD2_0
        foreach (PdfOperator value in Enum.GetValues(typeof(PdfOperator)))
#else
        foreach (PdfOperator value in Enum.GetValues<PdfOperator>())
#endif
        {
            sharedValues[value] = new PdfOperatorValue(value);
        }

        return sharedValues;
    }

    /// <inheritdoc/>
    public override string ToString() => GetOperatorText();
}
