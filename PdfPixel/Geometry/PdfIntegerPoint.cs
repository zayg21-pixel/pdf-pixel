using System.Globalization;

namespace PdfPixel.Geometry;

/// <summary>
/// An integer-valued point defined by its X and Y coordinates.
/// </summary>
public readonly struct PdfIntegerPoint
{
    /// <summary>
    /// Initializes a new <see cref="PdfIntegerPoint"/> from its coordinates.
    /// </summary>
    public PdfIntegerPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// The point at the origin.
    /// </summary>
    public static PdfIntegerPoint Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this point equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}]";
}
