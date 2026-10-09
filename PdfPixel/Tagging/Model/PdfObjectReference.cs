using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// A whole object (/OBJR), such as an annotation or an XObject, held by a structure element.
/// </summary>
public sealed class PdfObjectReference : IPdfStructureNode, IEquatable<PdfObjectReference>
{
    private readonly PdfStructureElement _parent;

    internal PdfObjectReference(PdfStructureElement parent, in PdfReference objectReference, PdfReference? pageReference)
    {
        _parent = parent;
        ObjectReference = objectReference;
        PageReference = pageReference;
    }

    /// <summary>
    /// The object (/Obj) this node refers to.
    /// </summary>
    public PdfReference ObjectReference { get; }

    /// <summary>
    /// Page (/Pg) the object appears on, falling back to the parent element's page,
    /// or <see langword="null"/> when neither is present.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <inheritdoc />
    public PdfStructureElement? GetParent() => _parent;

    /// <inheritdoc />
    public IEnumerable<IPdfStructureNode> EnumerateChildren()
    {
        yield break;
    }

    /// <summary>
    /// Determines whether two references name the same object.
    /// </summary>
    public bool Equals(PdfObjectReference? other)
    {
        if (other == null)
        {
            return false;
        }

        return ObjectReference == other.ObjectReference;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfObjectReference);

    /// <inheritdoc />
    public override int GetHashCode() => ObjectReference.GetHashCode();

    /// <summary>
    /// Determines whether two references name the same object.
    /// </summary>
    public static bool operator ==(PdfObjectReference? left, PdfObjectReference? right) => Equals(left, right);

    /// <summary>
    /// Determines whether two references name different objects.
    /// </summary>
    public static bool operator !=(PdfObjectReference? left, PdfObjectReference? right) => !Equals(left, right);
}
