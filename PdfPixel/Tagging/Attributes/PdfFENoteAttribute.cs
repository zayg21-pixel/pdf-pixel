using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Tagging.Attributes;

/// <summary>
/// Footnote and endnote attributes (/O /FENote, Well-Tagged PDF).
/// </summary>
public sealed class PdfFENoteAttribute : PdfStructureAttribute
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
