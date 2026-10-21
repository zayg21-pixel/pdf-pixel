using PdfPixel.Objects;

namespace PdfPixel.Measures;

/// <summary>
/// Preferred unit for displaying geospatial angles.
/// </summary>
[PdfEnum]
public enum PdfAngularDisplayUnit
{
    /// <summary>
    /// The value is not one of the defined units.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Degree.
    /// </summary>
    [PdfEnumValue("DEG")]
    Degree,

    /// <summary>
    /// Grad, 1/400 of a circle.
    /// </summary>
    [PdfEnumValue("GRD")]
    Grad
}
