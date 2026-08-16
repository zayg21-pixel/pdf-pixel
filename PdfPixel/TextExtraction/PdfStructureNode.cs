using PdfPixel.Models;

namespace PdfPixel.TextExtraction;

/// <summary>
/// A child entry (/K) of a <see cref="PdfStructureElement"/>.
/// </summary>
public readonly struct PdfStructureNode
{
    internal PdfStructureNode(PdfStructureElement element)
    {
        Type = PdfStructureNodeType.Element;
        Element = element;
    }

    internal PdfStructureNode(PdfStructureNodeType type, int? mcid, PdfReference? pageReference, PdfReference? streamReference, PdfReference? streamOwnerReference, PdfReference? objectReference)
    {
        Type = type;
        Mcid = mcid;
        PageReference = pageReference;
        StreamReference = streamReference;
        StreamOwnerReference = streamOwnerReference;
        ObjectReference = objectReference;
    }

    /// <summary>
    /// The kind of this node.
    /// </summary>
    public PdfStructureNodeType Type { get; }

    /// <summary>
    /// The nested structure element, or <see langword="null"/> when this node is not an element.
    /// </summary>
    public PdfStructureElement? Element { get; }

    /// <summary>
    /// The marked content identifier (/MCID), or <see langword="null"/> when this node is not marked content.
    /// </summary>
    public int? Mcid { get; }

    /// <summary>
    /// Page (/Pg) of the referenced content or object, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Content stream (/Stm) holding the marked content, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? StreamReference { get; }

    /// <summary>
    /// Object (/StmOwn) owning <see cref="StreamReference"/>, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? StreamOwnerReference { get; }

    /// <summary>
    /// The object (/Obj) this node refers to, or <see langword="null"/> when this node is not an object reference.
    /// </summary>
    public PdfReference? ObjectReference { get; }
}
