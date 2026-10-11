using PdfPixel.Text;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Page edge an artifact is logically attached to (Attached).
/// </summary>
[PdfEnum]
public enum PdfArtifactEdge
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Top edge of the crop box.
    /// </summary>
    [PdfEnumValue("Top")]
    Top,

    /// <summary>
    /// Bottom edge of the crop box.
    /// </summary>
    [PdfEnumValue("Bottom")]
    Bottom,

    /// <summary>
    /// Left edge of the crop box.
    /// </summary>
    [PdfEnumValue("Left")]
    Left,

    /// <summary>
    /// Right edge of the crop box.
    /// </summary>
    [PdfEnumValue("Right")]
    Right
}
