using System;

namespace PdfPixel.Geometry;

/// <summary>
/// Validates and normalizes quarter-turn rotation angles.
/// </summary>
internal static class PdfRotationUtilities
{
    /// <summary>
    /// Reduces <paramref name="rotation"/> to the 0-359 range.
    /// </summary>
    public static int Normalize(int rotation) => ((rotation % 360) + 360) % 360;

    /// <summary>
    /// Throws when <paramref name="rotation"/> is not a multiple of 90.
    /// </summary>
    public static void Validate(int rotation, string parameterName)
    {
        if (rotation % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, rotation, "Rotation must be a multiple of 90.");
        }
    }
}
