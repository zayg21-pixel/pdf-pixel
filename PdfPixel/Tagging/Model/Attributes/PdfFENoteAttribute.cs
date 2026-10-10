using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Footnote and endnote attributes (/O /FENote, Well-Tagged PDF).
/// </summary>
public sealed class PdfFENoteAttribute : PdfStructureAttributeBase
{
    internal PdfFENoteAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.FENote, revision)
    {
        NoteType = dictionary.GetName(PdfTokens.NoteTypeKey)?.AsEnum<PdfStructureNoteType>();
    }

    /// <summary>
    /// Type of the note (NoteType), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureNoteType? NoteType { get; }
}
