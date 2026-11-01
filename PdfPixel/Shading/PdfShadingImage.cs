using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the sampled color components and coordinate matrix produced by function-based (Type 1) shading.
/// </summary>
public sealed class PdfShadingImage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingImage"/> class.
    /// </summary>
    /// <param name="samples">Sampled shading components.</param>
    /// <param name="matrix">User-space mapping matrix.</param>
    internal PdfShadingImage(PdfImageSamples samples, in PdfMatrix matrix)
    {
        Samples = samples;
        Matrix = matrix;
    }

    /// <summary>
    /// Gets the float color components sampled over the shading domain.
    /// </summary>
    public PdfImageSamples Samples { get; }

    /// <summary>
    /// Gets the matrix that maps the image into user space.
    /// </summary>
    public PdfMatrix Matrix { get; }
}
