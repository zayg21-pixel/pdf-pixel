using PdfPixel.Text;

namespace PdfPixel.Files;

/// <summary>
/// File system interpreting a file specification (FS).
/// </summary>
[PdfEnum]
public enum PdfFileSystem
{
    /// <summary>
    /// File system not defined by a specification.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// The file name is a uniform resource locator.
    /// </summary>
    [PdfEnumValue("URL")]
    Url
}
