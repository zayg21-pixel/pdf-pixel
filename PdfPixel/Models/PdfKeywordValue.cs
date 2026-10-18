using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a file structure keyword, such as <c>obj</c>, <c>stream</c> or <c>trailer</c>.
/// </summary>
public sealed class PdfKeywordValue : PdfValue
{
    private static readonly Dictionary<PdfKeyword, PdfKeywordValue> SharedValues = CreateSharedValues();

    private PdfKeywordValue(PdfKeyword value) => Value = value;

    /// <summary>
    /// Gets the keyword.
    /// </summary>
    public PdfKeyword Value { get; }

    /// <summary>
    /// Returns the shared instance for a defined keyword, or a new instance for any other keyword code.
    /// </summary>
    /// <param name="value">The keyword.</param>
    internal static PdfKeywordValue FromKeyword(PdfKeyword value)
    {
        if (SharedValues.TryGetValue(value, out PdfKeywordValue? sharedValue))
        {
            return sharedValue;
        }

        return new PdfKeywordValue(value);
    }

    private static Dictionary<PdfKeyword, PdfKeywordValue> CreateSharedValues()
    {
        Dictionary<PdfKeyword, PdfKeywordValue> sharedValues = [];

#if NETSTANDARD2_0
        foreach (PdfKeyword value in Enum.GetValues(typeof(PdfKeyword)))
#else
        foreach (PdfKeyword value in Enum.GetValues<PdfKeyword>())
#endif
        {
            sharedValues[value] = new PdfKeywordValue(value);
        }

        return sharedValues;
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
