using PdfReader.Color.Structures;
using SkiaSharp;
using System;

namespace PdfReader.Color.Lut;

/// <summary>
/// Applies color transformation from source representation to destination RGBA representation.
/// </summary>
public interface IRgbaSampler
{
    /// <summary>
    /// True if this sampler is the default, otherwise, LUT-based sampling is used.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    /// Converts color the color data from the source array to the destination <see cref="RgbaPacked"/>
    /// structure.
    /// </summary>
    /// <param name="source">The source components array containing unconverted color.</param>
    /// <param name="destination">The destination <see cref="RgbaPacked"/> structure.</param>
    void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination);

    /// <summary>
    /// Samples the color from the source array and returns it as an <see cref="SKColor"/>.
    /// </summary>
    /// <param name="source">Source array.</param>
    /// <returns><see cref="SKColor"/> with converted values.</returns>
    SKColor SampleColor(ReadOnlySpan<float> source);
}
