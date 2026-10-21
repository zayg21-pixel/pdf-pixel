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
        Type = dictionary.GetName(PdfTokens.TypeKey)?.AsEnum<PdfArtifactType>();
        BoundingBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey));
        RawSubtype = dictionary.GetName(PdfTokens.SubtypeKey);
        Subtype = RawSubtype?.AsEnum<PdfArtifactSubtype>();
    }

    /// <summary>
    /// Type of artifact (Type), or <see langword="null"/> when absent.
    /// </summary>
    public PdfArtifactType? Type { get; }

    /// <summary>
    /// Bounding box of the artifact's visible extent (BBox), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? BoundingBox { get; }

    /// <summary>
    /// Subtype of artifact (Subtype), or <see langword="null"/> when absent.
    /// <see cref="PdfArtifactSubtype.Raw"/> when the subtype is not defined by a specification.
    /// </summary>
    public PdfArtifactSubtype? Subtype { get; }

    /// <summary>
    /// Subtype of artifact (Subtype) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawSubtype { get; }
}
