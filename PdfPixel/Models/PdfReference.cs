using System;

namespace PdfPixel.Models;

/// <summary>
/// Represents a reference to a PDF object.
/// </summary>
public readonly struct PdfReference : IEquatable<PdfReference>
{
    /// <summary>
    /// Initializes a PDF object reference with the given object number and generation.
    /// </summary>
    public PdfReference(uint objectNumber, int generation = 0)
    {
        ObjectNumber = objectNumber;
        Generation = generation;
    }

    /// <summary>
    /// Number of the referenced object.
    /// </summary>
    public uint ObjectNumber { get; }

    /// <summary>
    /// Object generation number.
    /// </summary>
    public int Generation { get; }

    /// <summary>
    /// True if this is a valid reference (object number > 0).
    /// </summary>
    public bool IsValid => ObjectNumber > 0;

    /// <inheritdoc/>
    public override string ToString() => $"{ObjectNumber} {Generation} R";

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ObjectNumber, Generation);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfReference other && other.ObjectNumber == ObjectNumber && other.Generation == Generation;

    /// <inheritdoc/>
    public bool Equals(PdfReference other) => other.ObjectNumber == ObjectNumber && other.Generation == Generation;

    /// <inheritdoc/>
    public static bool operator ==(in PdfReference left, in PdfReference right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(in PdfReference left, in PdfReference right) => !left.Equals(right);
}
