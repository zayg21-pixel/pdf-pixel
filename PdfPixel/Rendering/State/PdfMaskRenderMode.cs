namespace PdfPixel.Rendering.State;

/// <summary>
/// Specifies which channel of a soft mask is used when compositing a transparency group.
/// </summary>
public enum PdfMaskRenderMode
{
    /// <summary>
    /// No soft mask is applied.
    /// </summary>
    None,

    /// <summary>
    /// The alpha channel of the soft mask group is used as the mask values.
    /// </summary>
    Alpha,

    /// <summary>
    /// The luminosity of the soft mask group is used as the mask values.
    /// </summary>
    Luminosity
}
