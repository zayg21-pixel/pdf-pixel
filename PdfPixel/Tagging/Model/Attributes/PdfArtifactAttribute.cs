using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Artifact attributes (/O /Artifact, PDF 2.0).
/// </summary>
public sealed class PdfArtifactAttribute : PdfStructureAttributeBase
{
    internal PdfArtifactAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.Artifact, revision)
    {
        Type = dictionary.GetName(PdfTokens.TypeKey)?.AsEnum<PdfStructureArtifactType>();
        BBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey));
        RawSubtype = dictionary.GetName(PdfTokens.SubtypeKey);
        Subtype = RawSubtype?.AsEnum<PdfStructureArtifactSubtype>();
    }

    /// <summary>
    /// Type of artifact (Type), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureArtifactType? Type { get; }

    /// <summary>
    /// Bounding box of the artifact's visible extent (BBox), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? BBox { get; }

    /// <summary>
    /// Subtype of artifact (Subtype), or <see langword="null"/> when absent.
    /// <see cref="PdfStructureArtifactSubtype.Raw"/> when the subtype is not defined by a specification.
    /// </summary>
    public PdfStructureArtifactSubtype? Subtype { get; }

    /// <summary>
    /// Subtype of artifact (Subtype) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawSubtype { get; }
}
