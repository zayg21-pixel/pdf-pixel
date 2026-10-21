using PdfPixel.Objects;

namespace PdfPixel.Actions;

/// <summary>
/// Operation of a Windows launch (/O).
/// </summary>
[PdfEnum]
public enum PdfLaunchOperation
{
    /// <summary>
    /// Unknown or unsupported operation.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Open the document.
    /// </summary>
    [PdfEnumValue("open")]
    Open,

    /// <summary>
    /// Print the document.
    /// </summary>
    [PdfEnumValue("print")]
    Print
}
