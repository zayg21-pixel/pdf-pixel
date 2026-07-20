using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Utilities to convert to PDF color.
/// </summary>
internal static class PdfColorTransformUtilities
{
    /// <summary>
    /// Converts a Vector4 with color channel values in the range [0, 1] to a <see cref="PdfColor"/>.
    /// </summary>
    /// <param name="source">A Vector4 representing the source color, where the X, Y, and Z components correspond to the red, green, and blue
    /// channels, respectively. Each component is in the range [0, 1] by convention.</param>
    /// <returns>A <see cref="PdfColor"/> representing the equivalent color. The alpha channel is set to fully opaque.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfColor ToPdfColor(this Vector4 source) => new(source.X, source.Y, source.Z);
}
