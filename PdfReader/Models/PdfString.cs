using PdfReader.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents a PDF string value.
    /// </summary>
    public readonly struct PdfString : IEquatable<PdfString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfString"/> struct.
        /// </summary>
        /// <param name="value">The byte value of the PDF string.</param>
        public PdfString(ReadOnlyMemory<byte> value)
        {
            Value = value;
        }

        public PdfString(IList<byte> value)
        {
            Value = value.ToArray();
        }

        public PdfString(byte[] value)
        {
            Value = value;
        }

        public PdfString(ReadOnlySpan<byte> value)
        {
            Value = value.ToArray();
        }

        /// <summary>
        /// Gets the underlying byte value of the PDF string.
        /// </summary>
        public ReadOnlyMemory<byte> Value { get; }

        public bool IsEmpty => Value.Length == 0;

        public bool IsName => Value.Length > 0 && Value.Span[0] == (byte)'/';

        public static PdfString Empty => new PdfString(ReadOnlyMemory<byte>.Empty);

        public static PdfString FromString(string value)
        {
            var bytes = EncodingExtensions.PdfDefault.GetBytes(value);
            return new PdfString(bytes);
        }

        public PdfString TrimName()
        {
            if (IsName)
            {
                return Value.Slice(1);
            }

            return this;
        }

        /// <summary>
        /// Determines whether the specified <see cref="PdfString"/> is equal to the current <see cref="PdfString"/>
        /// </summary>
        /// <param name="other">The other <see cref="PdfString"/> to compare.</param>
        /// <returns>true if the values are equal; otherwise, false.</returns>
        public bool Equals(PdfString other)
        {
            return Value.Span.SequenceEqual(other.Value.Span);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="PdfString"/>.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>true if the object is a <see cref="PdfString"/> and the values are equal; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PdfString other)
            {
                return Equals(other);
            }
            return false;
        }

        /// <summary>
        /// Returns a hash code for the current <see cref="PdfString"/>.
        /// </summary>
        /// <returns>A hash code for the value.</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var b in Value.Span)
            {
                hash.Add(b);
            }
            return hash.ToHashCode();
        }

        /// <summary>
        /// Equality operator for <see cref="PdfString"/>.
        /// </summary>
        public static bool operator ==(PdfString left, PdfString right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator for <see cref="PdfString"/>.
        /// </summary>
        public static bool operator !=(PdfString left, PdfString right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Implicitly converts a ReadOnlyMemory&lt;byte&gt; to a PdfString.
        /// </summary>
        /// <param name="value">The byte memory to convert.</param>
        public static implicit operator PdfString(ReadOnlyMemory<byte> value)
        {
            return new PdfString(value);
        }

        /// <summary>
        /// Explicitly converts a ReadOnlySpan&lt;byte&gt; to a PdfString.
        /// </summary>
        /// <param name="value">The byte span to convert.</param>
        public static implicit operator PdfString(ReadOnlySpan<byte> value)
        {
            return new PdfString(value.ToArray());
        }

        /// <summary>
        /// Implicitly converts a PdfString to a string using PDF default encoding.
        /// </summary>
        [Obsolete("Use PdfString instead")]
        /// <param name="pdfString">The PdfString to convert.</param>
        public static implicit operator string(PdfString pdfString)
        {
            return pdfString.ToString();
        }

        public override string ToString()
        {
            return EncodingExtensions.PdfDefault.GetString(Value.Span);
        }
    }
}
