using PdfPixel.Objects;

namespace PdfPixel.Measures;

/// <summary>
/// Preferred unit for displaying geospatial areas.
/// </summary>
[PdfEnum]
public enum PdfAreaDisplayUnit
{
    /// <summary>
    /// The value is not one of the defined units.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Square meter.
    /// </summary>
    [PdfEnumValue("SQM")]
    SquareMeter,

    /// <summary>
    /// Hectare, 10,000 square meters.
    /// </summary>
    [PdfEnumValue("HA")]
    Hectare,

    /// <summary>
    /// Square kilometer.
    /// </summary>
    [PdfEnumValue("SQKM")]
    SquareKilometer,

    /// <summary>
    /// Square foot (U.S. survey).
    /// </summary>
    [PdfEnumValue("SQFT")]
    SquareFoot,

    /// <summary>
    /// Acre.
    /// </summary>
    [PdfEnumValue("A")]
    Acre,

    /// <summary>
    /// Square mile (international).
    /// </summary>
    [PdfEnumValue("SQMI")]
    SquareMile
}
