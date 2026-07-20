using PdfPixel.Models;
using System.Globalization;

namespace PdfPixel.Geometry;

/// <summary>
/// A point defined by its X and Y coordinates.
/// </summary>
public readonly struct PdfPoint
{
    /// <summary>
    /// Initializes a new <see cref="PdfPoint"/> from its coordinates.
    /// </summary>
    public PdfPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// The point at the origin.
    /// </summary>
    public static PdfPoint Empty { get; } = new(0, 0);

    /// <summary>
    /// Whether this point equals <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    /// <summary>
    /// Creates a <see cref="PdfPoint"/> from a 2-element PDF array.
    /// Returns null if the array is not defined or has insufficient elements.
    /// </summary>
    public static PdfPoint? FromArray(PdfArray? array)
    {
        if (array == null || array.Count < 2)
        {
            return null;
        }

        return new PdfPoint(array.GetFloatOrDefault(0), array.GetFloatOrDefault(1));
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{X.ToString(CultureInfo.InvariantCulture)} {Y.ToString(CultureInfo.InvariantCulture)}]";
}
