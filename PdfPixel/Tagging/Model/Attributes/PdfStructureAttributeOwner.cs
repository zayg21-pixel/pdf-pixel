using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Owner (/O) of a structure attribute object.
/// </summary>
[PdfEnum]
public enum PdfStructureAttributeOwner
{
    /// <summary>
    /// Owner not defined by a specification.
    /// </summary>
    [PdfEnumDefaultValue]
    Raw,

    /// <summary>
    /// Layout attributes.
    /// </summary>
    [PdfEnumValue("Layout")]
    Layout,

    /// <summary>
    /// List attributes.
    /// </summary>
    [PdfEnumValue("List")]
    List,

    /// <summary>
    /// Attributes of non-interactive form fields.
    /// </summary>
    [PdfEnumValue("PrintField")]
    PrintField,

    /// <summary>
    /// Table attributes.
    /// </summary>
    [PdfEnumValue("Table")]
    Table,

    /// <summary>
    /// Artifact attributes (PDF 2.0).
    /// </summary>
    [PdfEnumValue("Artifact")]
    Artifact,

    /// <summary>
    /// User properties (PDF 1.6).
    /// </summary>
    [PdfEnumValue("UserProperties")]
    UserProperties,

    /// <summary>
    /// Attributes owned by the namespace in /NS (PDF 2.0).
    /// </summary>
    [PdfEnumValue("NSO")]
    NamespaceOwner,

    /// <summary>
    /// Footnote and endnote attributes (Well-Tagged PDF).
    /// </summary>
    [PdfEnumValue("FENote")]
    FENote,

    /// <summary>
    /// Attributes for translation to XML.
    /// </summary>
    [PdfEnumValue("XML")]
    Xml,

    /// <summary>
    /// Attributes for translation to HTML.
    /// </summary>
    [PdfEnumValue("HTML")]
    Html,

    /// <summary>
    /// Attributes for translation to Open eBook.
    /// </summary>
    [PdfEnumValue("OEB")]
    Oeb,

    /// <summary>
    /// Attributes for translation to Rich Text Format.
    /// </summary>
    [PdfEnumValue("RTF")]
    Rtf,

    /// <summary>
    /// Attributes for translation to a format using CSS.
    /// </summary>
    [PdfEnumValue("CSS")]
    Css,

    /// <summary>
    /// Attributes for translation to a format using RDFa.
    /// </summary>
    [PdfEnumValue("RDFa")]
    Rdfa,

    /// <summary>
    /// Attributes for translation to a format using WAI-ARIA.
    /// </summary>
    [PdfEnumValue("ARIA")]
    Aria
}
