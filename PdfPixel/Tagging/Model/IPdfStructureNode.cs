using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model;

/// <summary>
/// A node of the document structure tree: a structure element or a content item it holds.
/// </summary>
public interface IPdfStructureNode
{
    /// <summary>
    /// Page (/Pg) the node's content appears on, or <see langword="null"/> when absent.
    /// </summary>
    PdfReference? PageReference { get; }

    /// <summary>
    /// Structure element holding this node, or <see langword="null"/> when the parent is the
    /// structure tree root.
    /// </summary>
    PdfStructureElement? GetParent();

    /// <summary>
    /// Children (/K) of this node in document order.
    /// </summary>
    IEnumerable<IPdfStructureNode> EnumerateChildren();
}
