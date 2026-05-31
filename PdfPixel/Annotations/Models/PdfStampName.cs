using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Standard rubber stamp annotation name values as defined in the PDF specification (Table 181).
/// </summary>
[PdfEnum]
public enum PdfStampName
{
    /// <summary>
    /// Unknown or non-standard stamp name.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Approved.
    /// </summary>
    [PdfEnumValue("Approved")]
    Approved,

    /// <summary>
    /// As Is.
    /// </summary>
    [PdfEnumValue("AsIs")]
    AsIs,

    /// <summary>
    /// Confidential.
    /// </summary>
    [PdfEnumValue("Confidential")]
    Confidential,

    /// <summary>
    /// Departmental.
    /// </summary>
    [PdfEnumValue("Departmental")]
    Departmental,

    /// <summary>
    /// Draft.
    /// </summary>
    [PdfEnumValue("Draft")]
    Draft,

    /// <summary>
    /// Experimental.
    /// </summary>
    [PdfEnumValue("Experimental")]
    Experimental,

    /// <summary>
    /// Expired.
    /// </summary>
    [PdfEnumValue("Expired")]
    Expired,

    /// <summary>
    /// Final.
    /// </summary>
    [PdfEnumValue("Final")]
    Final,

    /// <summary>
    /// For Comment.
    /// </summary>
    [PdfEnumValue("ForComment")]
    ForComment,

    /// <summary>
    /// For Public Release.
    /// </summary>
    [PdfEnumValue("ForPublicRelease")]
    ForPublicRelease,

    /// <summary>
    /// Not Approved.
    /// </summary>
    [PdfEnumValue("NotApproved")]
    NotApproved,

    /// <summary>
    /// Not For Public Release.
    /// </summary>
    [PdfEnumValue("NotForPublicRelease")]
    NotForPublicRelease,

    /// <summary>
    /// Sold.
    /// </summary>
    [PdfEnumValue("Sold")]
    Sold,

    /// <summary>
    /// Top Secret.
    /// </summary>
    [PdfEnumValue("TopSecret")]
    TopSecret
}
