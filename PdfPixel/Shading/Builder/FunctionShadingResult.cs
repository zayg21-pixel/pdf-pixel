using SkiaSharp;
using System;

namespace PdfPixel.Shading;

/// <summary>
/// Holds the sampled bitmap and coordinate matrix produced by function-based (Type 1) shading.
/// Disposes the bitmap when the instance is disposed.
/// </summary>
public sealed class FunctionShadingResult : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionShadingResult"/> class.
    /// </summary>
    /// <param name="bitmap">Sampled shading bitmap.</param>
    /// <param name="matrix">User-space mapping matrix.</param>
    public FunctionShadingResult(SKBitmap bitmap, SKMatrix matrix)
    {
        Bitmap = bitmap;
        Matrix = matrix;
    }

    /// <summary>
    /// Gets the sampled bitmap that represents the shading domain.
    /// </summary>
    public SKBitmap Bitmap { get; }

    /// <summary>
    /// Gets the matrix that maps the bitmap into user space.
    /// </summary>
    public SKMatrix Matrix { get; }

    /// <inheritdoc />
    public void Dispose() => Bitmap?.Dispose();
}
