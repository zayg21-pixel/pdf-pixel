using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Tagging.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Property list of an artifact marked content scope (Artifact BDC).
/// </summary>
public sealed class PdfArtifactProperties
{
    internal PdfArtifactProperties(PdfDictionary dictionary)
    {
        Type = dictionary.GetName(PdfTokens.TypeKey)?.AsEnum<PdfArtifactType>();
        BBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey));
        Attached = ReadAttached(dictionary.GetArray(PdfTokens.AttachedKey));
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
    public PdfRectangle? BBox { get; }

    /// <summary>
    /// Page edges the artifact is logically attached to (Attached), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfArtifactEdge>? Attached { get; }

    /// <summary>
    /// Subtype of artifact (Subtype), or <see langword="null"/> when absent.
    /// <see cref="PdfArtifactSubtype.Raw"/> when the subtype is not defined by a specification.
    /// </summary>
    public PdfArtifactSubtype? Subtype { get; }

    /// <summary>
    /// Subtype of artifact (Subtype) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawSubtype { get; }

    private static List<PdfArtifactEdge>? ReadAttached(PdfArray? attachedArray)
    {
        if (attachedArray == null)
        {
            return null;
        }

        List<PdfArtifactEdge> edges = new(attachedArray.Count);
        for (int index = 0; index < attachedArray.Count; index++)
        {
            PdfString? edgeName = attachedArray.GetName(index);
            if (edgeName != null)
            {
                edges.Add(edgeName.Value.AsEnum<PdfArtifactEdge>());
            }
        }

        return edges;
    }
}
