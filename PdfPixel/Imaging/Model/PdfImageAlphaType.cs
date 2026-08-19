namespace PdfPixel.Imaging.Model;

/// <summary>
/// Describes how alpha relates to the color samples it accompanies.
/// </summary>
public enum PdfImageAlphaType
{
    /// <summary>
    /// No alpha; every sample is fully opaque.
    /// </summary>
    Opaque,

    /// <summary>
    /// Color samples are multiplied by their alpha.
    /// </summary>
    Premultiplied,

    /// <summary>
    /// Color samples are independent of their alpha.
    /// </summary>
    Unpremultiplied
}
