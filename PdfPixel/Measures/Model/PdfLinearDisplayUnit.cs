using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Preferred unit for displaying geospatial positions and distances.
/// </summary>
[PdfEnum]
public enum PdfLinearDisplayUnit
{
    /// <summary>
    /// The value is not one of the defined units.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Meter.
    /// </summary>
    [PdfEnumValue("M")]
    Meter,

    /// <summary>
    /// Kilometer.
    /// </summary>
    [PdfEnumValue("KM")]
    Kilometer,

    /// <summary>
    /// International foot.
    /// </summary>
    [PdfEnumValue("FT")]
    InternationalFoot,

    /// <summary>
    /// U.S. survey foot.
    /// </summary>
    [PdfEnumValue("USFT")]
    UsSurveyFoot,

    /// <summary>
    /// International mile.
    /// </summary>
    [PdfEnumValue("MI")]
    InternationalMile,

    /// <summary>
    /// International nautical mile.
    /// </summary>
    [PdfEnumValue("NM")]
    NauticalMile
}
