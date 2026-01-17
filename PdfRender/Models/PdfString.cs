using PdfRender.Text;
using System;
using System.Linq;

namespace PdfRender.Models;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfString"/> class with the specified byte array value.
    /// </summary>
    /// <param name="value">The byte array representing the value of the PDF string. Cannot be <see langword="null"/>.</param>
    public PdfString(byte[] value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying byte value of the PDF string.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Gets a value indicating whether the current instance contains no characters.
    /// </summary>
    public bool IsEmpty => Value.Length == 0;

    /// <summary>
    /// Gets an empty <see cref="PdfString"/> instance.
    /// </summary>
    /// <remarks>This property provides a predefined, immutable instance of <see cref="PdfString"/> 
    /// that represents an empty string. It can be used as a default value or to avoid  creating new instances for
    /// empty content.</remarks>
    public static PdfString Empty => new PdfString(ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// Creates a new <see cref="PdfString"/> instance from the specified string value.
    /// </summary>
    /// <param name="value">The string to convert into a <see cref="PdfString"/>. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="PdfString"/> representing the specified string value.</returns>
    public static PdfString FromString(string value)
    {
        var bytes = EncodingExtensions.PdfDefault.GetBytes(value);
        return new PdfString(bytes);
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
    public static explicit operator PdfString(ReadOnlySpan<byte> value)
    {
        return new PdfString(value.ToArray());
    }

    /// <summary>
    /// Explicitly converts a string to a PdfString.
    /// </summary>
    /// <param name="value">String to convert.</param>
    public static explicit operator PdfString(string value)
    {
        return FromString(value);
    }

    public override string ToString()
    {
        return EncodingExtensions.PdfDefault.GetString(Value.Span);
    }
}
