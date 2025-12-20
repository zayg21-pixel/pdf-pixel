using PdfReader.Color.Structures;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Sampling;

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
    /// <param name="destination">The destination <see cref="RgbaPacked"/> structure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination);

    // TODO: add _pixelProcessorCallback method for bulk processing?
}
