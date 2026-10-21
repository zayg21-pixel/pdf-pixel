using PdfPixel.Objects;

namespace PdfPixel.Measures;

/// <summary>
/// Kind of coordinate system dictionary (/Type).
/// </summary>
[PdfEnum]
public enum PdfCoordinateSystemType
{
    /// <summary>
    /// The entry is absent or not one of the defined values.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Geographic coordinate system in angular units of latitude and longitude.
    /// </summary>
    [PdfEnumValue("GEOGCS")]
    Geographic,

    /// <summary>
    /// Projected coordinate system, a planar projection of an embedded geographic coordinate system.
    /// </summary>
    [PdfEnumValue("PROJCS")]
    Projected
}
