namespace PdfPixel.Tagging.Model;

/// <summary>
/// Identifies the concrete type of a <see cref="PdfScope"/>.
/// </summary>
public enum PdfScopeType
{
    /// <summary>
    /// <see cref="PdfContentStreamScope"/>.
    /// </summary>
    ContentStream,

    /// <summary>
    /// <see cref="PdfMarkedContentScope"/>.
    /// </summary>
    MarkedContent,

    /// <summary>
    /// <see cref="PdfXObjectScope"/>.
    /// </summary>
    XObject,

    /// <summary>
    /// <see cref="PdfAnnotationScope"/>.
    /// </summary>
    Annotation
}
