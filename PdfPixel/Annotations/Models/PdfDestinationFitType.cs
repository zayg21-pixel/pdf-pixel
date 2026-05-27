using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Defines how a destination page should be displayed.
/// </summary>
[PdfEnum]
public enum PdfDestinationFitType
{
    /// <summary>
    /// Unknown or unsupported fit type.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Display the page with explicit coordinates and zoom.
    /// </summary>
    [PdfEnumValue("XYZ")]
    XYZ,

    /// <summary>
    /// Fit the page in the window.
    /// </summary>
    [PdfEnumValue("Fit")]
    Fit,

    /// <summary>
    /// Fit the page width in the window.
    /// </summary>
    [PdfEnumValue("FitH")]
    FitH,

    /// <summary>
    /// Fit the page height in the window.
    /// </summary>
    [PdfEnumValue("FitV")]
    FitV,

    /// <summary>
    /// Fit a rectangle in the window.
    /// </summary>
    [PdfEnumValue("FitR")]
    FitR,

    /// <summary>
    /// Fit the page's bounding box in the window.
    /// </summary>
    [PdfEnumValue("FitB")]
    FitB,

    /// <summary>
    /// Fit the page's bounding box width in the window.
    /// </summary>
    [PdfEnumValue("FitBH")]
    FitBH,

    /// <summary>
    /// Fit the page's bounding box height in the window.
    /// </summary>
    [PdfEnumValue("FitBV")]
    FitBV
}
