using PdfPixel.Tagging;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// A node of the content tree holding the elements drawn inside one scope.
/// </summary>
public sealed class PdfContentNode : PdfContentElement
{
    private readonly List<PdfContentElement> _elements = [];

    /// <summary>
    /// Initializes a new <see cref="PdfContentNode"/> for the specified scope.
    /// </summary>
    internal PdfContentNode(PdfContentNode? parent, PdfScope? scope)
        : base(parent)
    {
        Scope = scope;
    }

    /// <summary>
    /// Scope that opened this node, or <see langword="null"/> for the root.
    /// </summary>
    public PdfScope? Scope { get; }

    /// <summary>
    /// Nodes and items of this node in content stream order.
    /// </summary>
    public IReadOnlyList<PdfContentElement> Elements => _elements;

    /// <inheritdoc />
    public override PdfContentElementType ElementType => PdfContentElementType.Node;

    internal void Add(PdfContentElement element) => _elements.Add(element);
}
