using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Type of coordinate system a measure dictionary measures in (/Subtype).
/// </summary>
[PdfEnum]
public enum PdfMeasureType
{
    /// <summary>
    /// A coordinate system no specification defines.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Rectilinear coordinate system with perpendicular, linearly incrementing axes.
    /// </summary>
    [PdfEnumValue("RL")]
    Rectilinear,

    /// <summary>
    /// Geospatial coordinate system relating the page to a model of the earth.
    /// </summary>
    [PdfEnumValue("GEO")]
    Geospatial
}
