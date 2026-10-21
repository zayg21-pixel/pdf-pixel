using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Type of a point data dictionary (/Subtype).
/// </summary>
[PdfEnum]
public enum PdfPointDataType
{
    /// <summary>
    /// The entry is absent or not one of the defined values.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Collection of point tuples without ordering or relationship between points.
    /// </summary>
    [PdfEnumValue("Cloud")]
    Cloud
}
