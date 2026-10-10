using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Name (/N) of a named action.
/// </summary>
[PdfEnum]
public enum PdfNamedActionName
{
    /// <summary>
    /// Name not defined by ISO 32000, such as a viewer-specific action; see <see cref="PdfNamedAction.RawName"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Go to the next page of the document.
    /// </summary>
    [PdfEnumValue("NextPage")]
    NextPage,

    /// <summary>
    /// Go to the previous page of the document.
    /// </summary>
    [PdfEnumValue("PrevPage")]
    PrevPage,

    /// <summary>
    /// Go to the first page of the document.
    /// </summary>
    [PdfEnumValue("FirstPage")]
    FirstPage,

    /// <summary>
    /// Go to the last page of the document.
    /// </summary>
    [PdfEnumValue("LastPage")]
    LastPage
}
