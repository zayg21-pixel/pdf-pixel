using System.Numerics;

namespace PdfReader.Color.Transform;

/// <summary>
/// Defines a universal interface for color transformation operations.
/// Implementations can transform any color vector, including grayscale, RGB, CMYK, and RGBA.
/// </summary>
public interface IColorTransform
{
    /// <summary>
    /// Returns true if the transformation is an identity operation (i.e., input colors remain unchanged).
    /// </summary>
    bool IsIdentity { get; }

    /// <summary>
    /// Transforms the specified color vector to another color space or representation.
    /// </summary>
    /// <param name="color">The input color as a <see cref="Vector4"/>. The interpretation of components depends on the color space (e.g., Gray, RGB, CMYK, RGBA).</param>
    /// <returns>The transformed color as a <see cref="Vector4"/>.</returns>
    Vector4 Transform(Vector4 color);
}
