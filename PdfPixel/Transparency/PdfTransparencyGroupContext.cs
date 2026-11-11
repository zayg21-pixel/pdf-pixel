using PdfPixel.Color.ColorSpace;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Transparency;

/// <summary>
/// A transparency group as it is painted: its attributes with its color space resolved. Two contexts are equal when
/// they have the same color space instance and the same attributes.
/// </summary>
public sealed class PdfTransparencyGroupContext : IEquatable<PdfTransparencyGroupContext>
{
    /// <summary>
    /// Initializes the context of a group.
    /// </summary>
    /// <param name="colorSpace">The group's color space, or <see langword="null"/> when the group declares none.</param>
    /// <param name="isolated">Whether the group's elements are composited onto a transparent backdrop.</param>
    /// <param name="knockout">Whether each element is composited onto the group's initial backdrop.</param>
    public PdfTransparencyGroupContext(PdfColorSpaceConverter? colorSpace, bool isolated, bool knockout)
    {
        ColorSpace = colorSpace;
        Isolated = isolated;
        Knockout = knockout;
    }

    /// <summary>
    /// Gets the group's color space, or <see langword="null"/> when the group declares none.
    /// </summary>
    public PdfColorSpaceConverter? ColorSpace { get; }

    /// <summary>
    /// Gets whether the group's elements are composited onto a fully transparent initial backdrop rather than
    /// onto the group's backdrop (ISO 32000-2 11.4.5).
    /// </summary>
    public bool Isolated { get; }

    /// <summary>
    /// Gets whether each of the group's elements is composited onto the group's initial backdrop rather than
    /// onto the elements before it (ISO 32000-2 11.4.6).
    /// </summary>
    public bool Knockout { get; }

    /// <inheritdoc />
    public bool Equals(PdfTransparencyGroupContext? other)
    {
        return other != null
            && ReferenceEquals(ColorSpace, other.ColorSpace)
            && Isolated == other.Isolated
            && Knockout == other.Knockout;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfTransparencyGroupContext);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int colorSpaceHash = (ColorSpace != null) ? RuntimeHelpers.GetHashCode(ColorSpace) : 0;

        return HashCode.Combine(colorSpaceHash, Isolated, Knockout);
    }
}
