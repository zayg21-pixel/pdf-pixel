using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Number format dictionary: a unit of measurement and the text display of values in it.
/// </summary>
public sealed class PdfNumberFormat
{
    private const int DefaultDecimalPrecision = 100;
    private const int DefaultFractionDenominator = 16;
    private const float DefaultConversionFactor = 1f;

    private static readonly PdfString DefaultThousandsSeparator = (PdfString)","u8;
    private static readonly PdfString DefaultDecimalSeparator = (PdfString)"."u8;
    private static readonly PdfString DefaultLabelSpacing = (PdfString)" "u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNumberFormat"/> class.
    /// </summary>
    /// <param name="dictionary">The number format dictionary.</param>
    internal PdfNumberFormat(PdfDictionary dictionary)
    {
        Unit = dictionary.GetStringOrDefault(PdfTokens.UnitLabelKey);
        ConversionFactor = dictionary.GetFloat(PdfTokens.ConversionFactorKey) ?? DefaultConversionFactor;
        FractionFormat = dictionary.GetNameOrDefault(PdfTokens.FractionFormatKey).AsEnum<PdfNumberFractionFormat>();

        int defaultPrecision = DefaultDecimalPrecision;
        if (FractionFormat == PdfNumberFractionFormat.Fraction)
        {
            defaultPrecision = DefaultFractionDenominator;
        }

        Precision = dictionary.GetInteger(PdfTokens.PrecisionKey) ?? defaultPrecision;
        FixedDenominator = dictionary.GetBooleanOrDefault(PdfTokens.FixedDenominatorKey);
        ThousandsSeparator = dictionary.GetString(PdfTokens.ThousandsSeparatorKey) ?? DefaultThousandsSeparator;

        PdfString? decimalSeparator = dictionary.GetString(PdfTokens.DecimalSeparatorKey);
        if (decimalSeparator == null || decimalSeparator.Value.IsEmpty)
        {
            DecimalSeparator = DefaultDecimalSeparator;
        }
        else
        {
            DecimalSeparator = decimalSeparator.Value;
        }

        LabelPrefix = dictionary.GetString(PdfTokens.LabelPrefixKey) ?? DefaultLabelSpacing;
        LabelSuffix = dictionary.GetString(PdfTokens.LabelSuffixKey) ?? DefaultLabelSpacing;
        LabelPosition = dictionary.GetNameOrDefault(PdfTokens.LabelPositionKey).AsEnum<PdfNumberLabelPosition>();
    }

    /// <summary>
    /// Label of the unit (/U). Empty when absent.
    /// </summary>
    public PdfString Unit { get; }

    /// <summary>
    /// Factor converting a value in the previous format's units, or in the measure's base units for the
    /// first format of an array, to this format's units (/C). Default 1.
    /// </summary>
    public float ConversionFactor { get; }

    /// <summary>
    /// Display of the fractional part of a value (/F). Default <see cref="PdfNumberFractionFormat.Decimal"/>.
    /// </summary>
    public PdfNumberFractionFormat FractionFormat { get; }

    /// <summary>
    /// Decimal precision or fraction denominator (/D). Default 100 for decimals, 16 for fractions.
    /// </summary>
    public int Precision { get; }

    /// <summary>
    /// Whether a fraction keeps its denominator unreduced and a decimal keeps its low-order zeros (/FD).
    /// </summary>
    public bool FixedDenominator { get; }

    /// <summary>
    /// Text between orders of thousands (/RT). Default comma.
    /// </summary>
    public PdfString ThousandsSeparator { get; }

    /// <summary>
    /// Text at the decimal position (/RD). Default period.
    /// </summary>
    public PdfString DecimalSeparator { get; }

    /// <summary>
    /// Text before the unit label (/PS). Default a single space.
    /// </summary>
    public PdfString LabelPrefix { get; }

    /// <summary>
    /// Text after the unit label (/SS). Default a single space.
    /// </summary>
    public PdfString LabelSuffix { get; }

    /// <summary>
    /// Position of the unit label relative to the value (/O). Default <see cref="PdfNumberLabelPosition.Suffix"/>.
    /// </summary>
    public PdfNumberLabelPosition LabelPosition { get; }

    /// <summary>
    /// Parses a number format array, in descending order of granularity.
    /// </summary>
    /// <param name="array">The number format array, or null.</param>
    /// <returns>The number formats, or <see langword="null"/> when <paramref name="array"/> is null.</returns>
    internal static PdfNumberFormat[]? FromArray(PdfArray? array)
    {
        if (array == null)
        {
            return null;
        }

        List<PdfNumberFormat> formats = new(array.Count);
        for (int index = 0; index < array.Count; index++)
        {
            PdfDictionary? formatDictionary = array.GetDictionary(index);
            if (formatDictionary != null)
            {
                formats.Add(new PdfNumberFormat(formatDictionary));
            }
        }

        return formats.ToArray();
    }
}
