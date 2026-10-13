namespace PdfPixel.Commands.Model;

/// <summary>
/// Kind of painted content an <see cref="IPdfContentCommand"/> carries.
/// </summary>
public enum PdfContentKind
{
    /// <summary>
    /// Text shown by a text showing operator.
    /// </summary>
    Text,

    /// <summary>
    /// Path painted by a path painting operator.
    /// </summary>
    Path,

    /// <summary>
    /// Image XObject or inline image.
    /// </summary>
    Image,

    /// <summary>
    /// Shading painted by the sh operator.
    /// </summary>
    Shading
}
