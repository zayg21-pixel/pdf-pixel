namespace PdfPixel.Content.Model;

/// <summary>
/// An element of the content tree built during command execution.
/// </summary>
public abstract class PdfContentElement
{
    /// <summary>
    /// Initializes a new <see cref="PdfContentElement"/> with the node it belongs to.
    /// </summary>
    private protected PdfContentElement(PdfContentNode? parent) => Parent = parent;

    /// <summary>
    /// Identifies the concrete type of this element.
    /// </summary>
    public abstract PdfContentElementType ElementType { get; }

    /// <summary>
    /// Node this element belongs to, or <see langword="null"/> for the root.
    /// </summary>
    public PdfContentNode? Parent { get; }
}
