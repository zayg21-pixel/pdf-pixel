namespace PdfReader.Models;

/// <summary>
/// Rendering parameters for <see cref="PdfPage"/>.
/// </summary>
public class PdfRenderingParameters
{

    /// <summary>
    /// Simplified more with lower rendering quality.
    /// </summary>
    public bool PreviewMode { get; set; }

    /// <summary>
    /// Actual device scale factor, if defined, all images will be downscaled
    /// to fit exact device scale, otherwise decoded in full size.
    /// </summary>
    public float? ScaleFactor { get; set; }
}
