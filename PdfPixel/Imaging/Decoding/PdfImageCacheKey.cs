using System;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Identifies decoded data of one image in an <see cref="IPdfImageCache"/>: which plane, at which reduction, which part.
/// </summary>
public readonly struct PdfImageCacheKey : IEquatable<PdfImageCacheKey>
{
    /// <summary>
    /// Initializes a key of <paramref name="part"/> of <paramref name="plane"/> decoded at
    /// <paramref name="descaleFactor"/>.
    /// </summary>
    /// <param name="plane">The plane: 0 for the colors, 1 for an opacity channel decoded apart.</param>
    /// <param name="descaleFactor">The power-of-two reduction the samples are decoded at; 1 for the stored size.</param>
    /// <param name="part">The tile index, or 0 for samples decoded whole.</param>
    public PdfImageCacheKey(int plane, int descaleFactor, int part)
    {
        Plane = plane;
        DescaleFactor = descaleFactor;
        Part = part;
    }

    /// <summary>
    /// Gets the plane: 0 for the colors, 1 for an opacity channel decoded apart.
    /// </summary>
    public int Plane { get; }

    /// <summary>
    /// Gets the power-of-two reduction the samples are decoded at; 1 for the stored size.
    /// </summary>
    public int DescaleFactor { get; }

    /// <summary>
    /// Gets the tile index, or 0 for samples decoded whole.
    /// </summary>
    public int Part { get; }

    /// <inheritdoc />
    public bool Equals(PdfImageCacheKey other)
    {
        return Plane == other.Plane
            && DescaleFactor == other.DescaleFactor
            && Part == other.Part;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfImageCacheKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Plane, DescaleFactor, Part);

    /// <summary>
    /// Determines whether two keys identify the same data.
    /// </summary>
    public static bool operator ==(in PdfImageCacheKey left, in PdfImageCacheKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two keys identify different data.
    /// </summary>
    public static bool operator !=(in PdfImageCacheKey left, in PdfImageCacheKey right) => !left.Equals(right);
}
