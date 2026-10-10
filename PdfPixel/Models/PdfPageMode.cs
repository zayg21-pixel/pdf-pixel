using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// How the document is displayed when opened (catalog /PageMode).
/// </summary>
[PdfEnum]
public enum PdfPageMode
{
    /// <summary>
    /// Unrecognized value.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Neither document outline nor thumbnail images visible.
    /// </summary>
    [PdfEnumValue("UseNone")]
    UseNone,

    /// <summary>
    /// Document outline visible.
    /// </summary>
    [PdfEnumValue("UseOutlines")]
    UseOutlines,

    /// <summary>
    /// Thumbnail images visible.
    /// </summary>
    [PdfEnumValue("UseThumbs")]
    UseThumbs,

    /// <summary>
    /// Full-screen mode, with no menu bar, window controls, or any other window visible.
    /// </summary>
    [PdfEnumValue("FullScreen")]
    FullScreen,

    /// <summary>
    /// Optional content group panel visible.
    /// </summary>
    [PdfEnumValue("UseOC")]
    UseOC,

    /// <summary>
    /// Attachments panel visible.
    /// </summary>
    [PdfEnumValue("UseAttachments")]
    UseAttachments
}
