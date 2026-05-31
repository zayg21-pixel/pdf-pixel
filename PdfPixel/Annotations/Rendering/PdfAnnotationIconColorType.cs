namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// How a path's colors are sourced at render time.
/// </summary>
internal enum PdfAnnotationIconColorType
{
    /// <summary>
    /// Do not apply this paint operation.
    /// </summary>
    None,

    /// <summary>
    /// Use the annotation's interior color.
    /// </summary>
    Interior,

    /// <summary>
    /// Use the annotation's exterior (border) color.
    /// </summary>
    Exterior,

    /// <summary>
    /// Use the color defined directly in the icon resource.
    /// </summary>
    Override
}
