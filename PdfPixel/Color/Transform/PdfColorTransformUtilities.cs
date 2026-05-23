using SkiaSharp;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Utilities to convert to PDF color.
/// </summary>
internal static class PdfColorTransformUtilities
{
    private static readonly Vector4 MaxByte = new Vector4(255f);
    private static readonly Vector4 ByteOffset = new Vector4(0.5f);

    /// <summary>
    /// Converts a Vector4 with color channel values in the range [0, 1] to an SKColor with 8-bit per channel RGB
    /// values.
    /// </summary>
    /// <param name="source">A Vector4 representing the source color, where the X, Y, and Z components correspond to the red, green, and blue
    /// channels, respectively. Each component should be in the range [0, 1].</param>
    /// <returns>An SKColor representing the equivalent color with 8-bit RGB channels. The alpha channel is set to fully opaque.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor From01ToSkiaColor(this Vector4 source)
    {
        var scaled = Vector4.Clamp(source * 255f, Vector4.Zero, MaxByte) + ByteOffset;
        return new SKColor(
            (byte)scaled.X,
            (byte)scaled.Y,
            (byte)scaled.Z,
            255);
    }
}
