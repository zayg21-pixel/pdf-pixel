using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using System;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the sampled image and coordinate matrix produced by function-based (Type 1) shading.
/// Disposes the image when the instance is disposed.
/// </summary>
public sealed class FunctionShadingResult : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionShadingResult"/> class.
    /// </summary>
    /// <param name="image">Sampled shading image.</param>
    /// <param name="matrix">User-space mapping matrix.</param>
    public FunctionShadingResult(PdfDecodedImage image, in PdfMatrix matrix)
    {
        Image = image;
        Matrix = matrix;
    }

    /// <summary>
    /// Gets the sampled image that represents the shading domain.
    /// </summary>
    public PdfDecodedImage Image { get; }

    /// <summary>
    /// Gets the matrix that maps the image into user space.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <inheritdoc />
    public void Dispose() => Image?.Dispose();
}
