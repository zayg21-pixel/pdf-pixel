using PdfPixel.Models;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Attributes applied only when exporting to a specific format, such as XML-1.00 or CSS-3.
/// </summary>
public sealed class PdfExportFormatAttribute : PdfStructureAttributeBase
{
    internal PdfExportFormatAttribute(PdfDictionary dictionary, PdfStructureAttributeOwner owner, in PdfString version, int? revision)
        : base(owner, revision)
    {
        Version = version;
        Attributes = dictionary;
    }

    /// <summary>
    /// Version of the export format as written in the owner (/O) name, such as "4.01" for HTML-4.01.
    /// </summary>
    public PdfString Version { get; }

    /// <summary>
    /// Attribute names and values defined by the export format.
    /// </summary>
    public PdfDictionary Attributes { get; }
}
