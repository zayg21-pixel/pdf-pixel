namespace PdfPixel.Content.Model;

/// <summary>
/// Identifies the concrete type of a <see cref="PdfContentElement"/>.
/// </summary>
public enum PdfContentElementType
{
    /// <summary>
    /// <see cref="PdfContentNode"/>.
    /// </summary>
    Node,

    /// <summary>
    /// <see cref="PdfContentItem"/>.
    /// </summary>
    Item
}
