using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// List attributes (/O /List).
/// </summary>
public sealed class PdfListAttribute : PdfStructureAttributeBase
{
    internal PdfListAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.List, revision)
    {
        ListNumbering = dictionary.GetName(PdfTokens.ListNumberingKey)?.AsEnum<PdfStructureListNumbering>();
        ContinuedList = dictionary.GetBoolean(PdfTokens.ContinuedListKey);
        ContinuedFrom = dictionary.GetString(PdfTokens.ContinuedFromKey);
    }

    /// <summary>
    /// Numbering system or bullet of the list (ListNumbering), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureListNumbering? ListNumbering { get; }

    /// <summary>
    /// Whether the list continues a previous list (ContinuedList, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public bool? ContinuedList { get; }

    /// <summary>
    /// Identifier (/ID) of the list this list continues (ContinuedFrom, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ContinuedFrom { get; }
}
