using System;
using System.Globalization;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// What one stored channel of a pixel holds: a color component or alpha.
/// </summary>
public readonly struct PdfImageChannel : IEquatable<PdfImageChannel>
{
    private PdfImageChannel(int? componentIndex) => ComponentIndex = componentIndex;

    /// <summary>
    /// Gets the alpha channel.
    /// </summary>
    public static PdfImageChannel Alpha => default;

    /// <summary>
    /// Gets the index of the color component the channel holds, or null for alpha.
    /// </summary>
    public int? ComponentIndex { get; }

    /// <summary>
    /// Gets whether the channel holds alpha.
    /// </summary>
    public bool IsAlpha => ComponentIndex == null;

    /// <summary>
    /// Returns the channel holding the color component at <paramref name="index"/>.
    /// </summary>
    public static PdfImageChannel Component(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Component index must not be negative.");
        }

        return new PdfImageChannel(index);
    }

    /// <inheritdoc />
    public bool Equals(PdfImageChannel other) => ComponentIndex == other.ComponentIndex;

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PdfImageChannel other)
        {
            return Equals(other);
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode() => ComponentIndex.GetHashCode();

    /// <inheritdoc />
    public override string ToString()
    {
        if (ComponentIndex is int index)
        {
            return "C" + index.ToString(CultureInfo.InvariantCulture);
        }

        return "A";
    }

    /// <summary>
    /// Equality operator for <see cref="PdfImageChannel"/>.
    /// </summary>
    public static bool operator ==(in PdfImageChannel left, in PdfImageChannel right) => left.Equals(right);

    /// <summary>
    /// Inequality operator for <see cref="PdfImageChannel"/>.
    /// </summary>
    public static bool operator !=(in PdfImageChannel left, in PdfImageChannel right) => !left.Equals(right);
}
