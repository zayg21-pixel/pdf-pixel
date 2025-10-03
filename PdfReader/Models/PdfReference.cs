using System;

namespace PdfReader.Models
{
    // Strongly-typed PDF reference
    public readonly struct PdfReference : IEquatable<PdfReference>
    {
        public int ObjectNumber { get; }
        public int Generation { get; }

        public PdfReference(int objectNumber, int generation = 0)
        {
            ObjectNumber = objectNumber;
            Generation = generation;
        }

        public bool IsValid => ObjectNumber > 0;

        public override string ToString() => $"{ObjectNumber} {Generation} R";

        public override int GetHashCode()
        {
            int hash = 23;
            hash = hash * 31 + ObjectNumber;
            hash = hash * 31 + Generation;

            return hash;
        }

        public bool Equals(PdfReference other)
        {
            return other.ObjectNumber == ObjectNumber && other.Generation == Generation;
        }
    }
}