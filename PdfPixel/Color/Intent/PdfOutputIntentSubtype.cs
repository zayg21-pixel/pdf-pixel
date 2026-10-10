using PdfPixel.Text;

namespace PdfPixel.Color.Intent;

/// <summary>
/// Output intent subtype (/S) of an output intent dictionary.
/// </summary>
[PdfEnum]
public enum PdfOutputIntentSubtype
{
    /// <summary>
    /// Subtype not defined by ISO 32000, such as one added by an extension; see <see cref="PdfOutputIntent.RawSubtype"/>.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// PDF/X output intent (ISO 15930).
    /// </summary>
    [PdfEnumValue("GTS_PDFX")]
    GtsPdfX,

    /// <summary>
    /// PDF/A output intent (ISO 19005).
    /// </summary>
    [PdfEnumValue("GTS_PDFA1")]
    GtsPdfA1,

    /// <summary>
    /// PDF/E output intent (ISO 24517).
    /// </summary>
    [PdfEnumValue("ISO_PDFE1")]
    IsoPdfE1
}
