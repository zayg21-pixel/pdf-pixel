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
    /// Converts the <see cref="InputComponentCount"/> components at <paramref name="input"/> into the
    /// <see cref="OutputComponentCount"/> components at <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The first component to convert.</param>
    /// <param name="output">The first component receiving the converted color.</param>
    void Transform(ref float input, ref float output);
}
