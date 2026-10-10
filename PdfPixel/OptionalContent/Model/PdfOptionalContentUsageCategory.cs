using PdfPixel.Text;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Usage dictionary entry consulted by a usage application (/Category).
/// </summary>
[PdfEnum]
public enum PdfOptionalContentUsageCategory
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Application-specific creator data (/CreatorInfo).
    /// </summary>
    [PdfEnumValue("CreatorInfo")]
    CreatorInfo,

    /// <summary>
    /// Language of the content (/Language).
    /// </summary>
    [PdfEnumValue("Language")]
    Language,

    /// <summary>
    /// Recommended state on export (/Export).
    /// </summary>
    [PdfEnumValue("Export")]
    Export,

    /// <summary>
    /// Magnification range (/Zoom).
    /// </summary>
    [PdfEnumValue("Zoom")]
    Zoom,

    /// <summary>
    /// Recommended state on print (/Print).
    /// </summary>
    [PdfEnumValue("Print")]
    Print,

    /// <summary>
    /// Recommended state on view (/View).
    /// </summary>
    [PdfEnumValue("View")]
    View,

    /// <summary>
    /// Intended users (/User).
    /// </summary>
    [PdfEnumValue("User")]
    User,

    /// <summary>
    /// Pagination artifact kind (/PageElement).
    /// </summary>
    [PdfEnumValue("PageElement")]
    PageElement
}
