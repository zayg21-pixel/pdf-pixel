using PdfPixel.Models;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Attribute object whose owner (/O) is not defined by a specification.
/// </summary>
public sealed class PdfRawStructureAttribute : PdfStructureAttributeBase
{
    internal PdfRawStructureAttribute(PdfDictionary dictionary, PdfString? rawOwner, int? revision)
        : base(PdfStructureAttributeOwner.Raw, revision)
    {
        RawOwner = rawOwner;
        Attributes = dictionary;
    }

    /// <summary>
    /// Owner of the attribute object (/O) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawOwner { get; }

    /// <summary>
    /// Attribute names and values of the attribute object.
    /// </summary>
    public PdfDictionary Attributes { get; }
}
