namespace PdfPixel.TextExtraction;

/// <summary>
/// The kind of child a structure element holds.
/// </summary>
public enum PdfStructureNodeType
{
    /// <summary>
    /// A nested structure element.
    /// </summary>
    Element,

    /// <summary>
    /// Marked content in a content stream, named by its identifier.
    /// </summary>
    Mcid,

    /// <summary>
    /// A whole object, such as an annotation or an XObject.
    /// </summary>
    ObjectReference
}
