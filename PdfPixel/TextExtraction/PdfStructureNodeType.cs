namespace PdfPixel.TextExtraction;

/// <summary>
/// Discriminates the kind of child entry in a structure element's /K array.
/// </summary>
public enum PdfStructureNodeType
{
    /// <summary>
    /// A nested structure element (/StructElem).
    /// </summary>
    Element,

    /// <summary>
    /// A marked content reference (MCID integer).
    /// </summary>
    Mcid
}
