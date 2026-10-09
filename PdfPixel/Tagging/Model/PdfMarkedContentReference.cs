using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// Marked content (/MCR or a bare /MCID) held by a structure element.
/// </summary>
public sealed class PdfMarkedContentReference : IPdfStructureNode, IEquatable<PdfMarkedContentReference>
{
    private readonly PdfStructureElement _parent;

    internal PdfMarkedContentReference(PdfStructureElement parent, int mcid, PdfReference? pageReference, PdfReference? streamReference, PdfReference? streamOwner)
    {
        _parent = parent;
        Mcid = mcid;
        PageReference = pageReference;
        StreamReference = streamReference;
        StreamOwner = streamOwner;
    }

    /// <summary>
    /// Marked content identifier (/MCID).
    /// </summary>
    public int Mcid { get; }

    /// <summary>
    /// Page (/Pg) the marked content appears on, falling back to the parent element's page,
    /// or <see langword="null"/> when neither is present.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Content stream (/Stm) holding the marked content, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? StreamReference { get; }

    /// <summary>
    /// Object (/StmOwn) owning <see cref="StreamReference"/>, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? StreamOwner { get; }

    /// <inheritdoc />
    public PdfStructureElement? GetParent() => _parent;

    /// <inheritdoc />
    public IEnumerable<IPdfStructureNode> EnumerateChildren()
    {
        yield break;
    }

    /// <summary>
    /// Determines whether two references name the same marked content.
    /// </summary>
    public bool Equals(PdfMarkedContentReference? other)
    {
        if (other == null)
        {
            return false;
        }

        return Mcid == other.Mcid
            && Nullable.Equals(PageReference, other.PageReference)
            && Nullable.Equals(StreamReference, other.StreamReference);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfMarkedContentReference);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Mcid, PageReference, StreamReference);

    /// <summary>
    /// Determines whether two references name the same marked content.
    /// </summary>
    public static bool operator ==(PdfMarkedContentReference? left, PdfMarkedContentReference? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two references name different marked content.
    /// </summary>
    public static bool operator !=(PdfMarkedContentReference? left, PdfMarkedContentReference? right) => !Equals(left, right);
}
