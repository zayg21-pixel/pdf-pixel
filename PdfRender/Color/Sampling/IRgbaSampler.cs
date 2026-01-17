using PdfRender.Color.Structures;
using System;
using System.Numerics;

namespace PdfRender.Color.Sampling;

/// <summary>
/// Applies color transformation from source representation to destination RGBA representation.
/// </summary>
public interface IRgbaSampler
{
    /// <summary>
    /// Converts color the color data from the source array to the destination <see cref="RgbaPacked"/>
    /// structure.
    /// </summary>
    /// <param name="source">The source components array containing unconverted color.</param>
    /// <returns><see cref="Vector4"/> that represents converted to RGBa color components, note, that they are not guaranteed to be normalized to 0-1.</returns>
    Vector4 Sample(ReadOnlySpan<float> source);
}
