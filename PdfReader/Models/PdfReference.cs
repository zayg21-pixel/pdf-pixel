using System;

namespace PdfReader.Models;

/// <summary>
/// Represents a reference to a PDF object.
/// </summary>
public readonly struct PdfReference : IEquatable<PdfReference>
{
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

    public override string ToString() => $"{ObjectNumber} {Generation} R";

    public override int GetHashCode()
    {
        return HashCode.Combine(ObjectNumber, Generation);
    }

    public bool Equals(PdfReference other)
    {
        return other.ObjectNumber == ObjectNumber && other.Generation == Generation;
    }
}