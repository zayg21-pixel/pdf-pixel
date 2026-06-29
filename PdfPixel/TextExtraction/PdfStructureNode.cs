using PdfPixel.Models;

namespace PdfPixel.TextExtraction;

/// <summary>
/// A child entry in a <see cref="PdfStructureElement"/>.
/// Either a nested structure element or a marked content reference (MCID).
/// </summary>
public readonly struct PdfStructureNode
{
    private PdfStructureNode(PdfStructureNodeType type, PdfStructureElement? element, int? mcid, PdfReference? pageReference)
    {
        Type = type;
        Element = element;
        Mcid = mcid;
        PageReference = pageReference;
    }

    /// <summary>
    /// The kind of this node.
    /// </summary>
    public PdfStructureNodeType Type { get; }

    /// <summary>
    /// The nested structure element, or <see langword="null"/> if this is an MCID reference.
    /// </summary>
    public PdfStructureElement? Element { get; }

    /// <summary>
    /// The marked content identifier, or <see langword="null"/> if this is a structure element.
    /// </summary>
    public int? Mcid { get; }

    /// <summary>
    /// Page reference from the MCR's own /Pg entry, overriding the parent element's page.
    /// <see langword="null"/> when absent — the parent element's page reference applies.
    /// </summary>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Creates a node wrapping a nested structure element.
    /// </summary>
    public static PdfStructureNode FromElement(PdfStructureElement element)
        => new(PdfStructureNodeType.Element, element, null, null);

    /// <summary>
    /// Creates a node wrapping a marked content identifier.
    /// </summary>
    public static PdfStructureNode FromMcid(int mcid, PdfReference? pageReference = null)
        => new(PdfStructureNodeType.Mcid, null, mcid, pageReference);
}
