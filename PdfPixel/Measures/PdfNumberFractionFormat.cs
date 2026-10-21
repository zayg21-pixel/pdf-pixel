using PdfPixel.Objects;

namespace PdfPixel.Measures;

/// <summary>
/// Display of the fractional part of a formatted measurement value (/F).
/// </summary>
[PdfEnum]
public enum PdfNumberFractionFormat
{
    /// <summary>
    /// Decimal to the precision given by <see cref="PdfNumberFormat.Precision"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    [PdfEnumValue("D")]
    Decimal,

    /// <summary>
    /// Fraction with the denominator given by <see cref="PdfNumberFormat.Precision"/>.
    /// </summary>
    [PdfEnumValue("F")]
    Fraction,

    /// <summary>
    /// No fractional part, rounded to the nearest whole unit.
    /// </summary>
    [PdfEnumValue("R")]
    Round,

    /// <summary>
    /// No fractional part, truncated to whole units.
    /// </summary>
    [PdfEnumValue("T")]
    Truncate
}
