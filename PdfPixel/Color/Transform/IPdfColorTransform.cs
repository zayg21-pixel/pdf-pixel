using System;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts the components of one color.
/// </summary>
public interface IPdfColorTransform
{
    /// <summary>
    /// Gets the number of components the transform reads.
    /// </summary>
    int InputComponentCount { get; }

    /// <summary>
    /// Gets the number of components the transform produces.
    /// </summary>
    int OutputComponentCount { get; }

    /// <summary>
    /// Converts the components in <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Components to convert.</param>
    /// <returns>The converted components.</returns>
    ReadOnlySpan<float> Transform(ReadOnlySpan<float> input);
}
