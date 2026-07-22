using System;
using System.IO.Hashing;
using System.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Represents a byte string used within font data (glyph names, CFF strings, Type1 dictionary values).
/// </summary>
public readonly struct PdfFontString : IEquatable<PdfFontString>
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFontString"/> struct.
    /// </summary>
    /// <param name="value">The byte value of the string.</param>
    public PdfFontString(in ReadOnlyMemory<byte> value) => Value = value;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFontString"/> struct with the specified byte array value.
    /// </summary>
    /// <param name="value">The byte array representing the value of the string. Cannot be <see langword="null"/>.</param>
    public PdfFontString(byte[] value) => Value = value;

    /// <summary>
    /// Gets the underlying byte value of the string.
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// Gets a value indicating whether the current instance contains no characters.
    /// </summary>
    public bool IsEmpty => Value.Length == 0;

    /// <summary>
    /// Gets an empty <see cref="PdfFontString"/> instance.
    /// </summary>
    public static PdfFontString Empty => new(ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// Creates a new <see cref="PdfFontString"/> instance from the specified string value.
    /// </summary>
    /// <param name="value">The string to convert into a <see cref="PdfFontString"/>. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="PdfFontString"/> representing the specified string value.</returns>
    public static PdfFontString FromString(string value)
    {
        byte[] bytes = Latin1.GetBytes(value);
        return new PdfFontString(bytes);
    }

    /// <summary>
    /// Determines whether the specified <see cref="PdfFontString"/> is equal to the current <see cref="PdfFontString"/>.
    /// </summary>
    /// <param name="other">The other <see cref="PdfFontString"/> to compare.</param>
    /// <returns>true if the values are equal; otherwise, false.</returns>
    public bool Equals(PdfFontString other) => Value.Span.SequenceEqual(other.Value.Span);

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="PdfFontString"/>.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>true if the object is a <see cref="PdfFontString"/> and the values are equal; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is PdfFontString other)
        {
            return Equals(other);
        }

        return false;
    }

    /// <summary>
    /// Returns a hash code for the current <see cref="PdfFontString"/>.
    /// </summary>
    /// <returns>A hash code for the value.</returns>
    public override int GetHashCode() => (int)XxHash32.HashToUInt32(Value.Span);

    /// <summary>
    /// Equality operator for <see cref="PdfFontString"/>.
    /// </summary>
    public static bool operator ==(in PdfFontString left, in PdfFontString right) => left.Equals(right);

    /// <summary>
    /// Inequality operator for <see cref="PdfFontString"/>.
    /// </summary>
    public static bool operator !=(in PdfFontString left, in PdfFontString right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts a ReadOnlyMemory&lt;byte&gt; to a PdfFontString.
    /// </summary>
    /// <param name="value">The byte memory to convert.</param>
    public static implicit operator PdfFontString(in ReadOnlyMemory<byte> value) => new(value);

    /// <summary>
    /// Explicitly converts a ReadOnlySpan&lt;byte&gt; to a PdfFontString.
    /// </summary>
    /// <param name="value">The byte span to convert.</param>
    public static explicit operator PdfFontString(in ReadOnlySpan<byte> value) => new(value.ToArray());

    /// <summary>
    /// Explicitly converts a string to a PdfFontString.
    /// </summary>
    /// <param name="value">String to convert.</param>
    public static explicit operator PdfFontString(string value) => FromString(value);

    /// <inheritdoc/>
    public override string ToString()
    {
        ReadOnlySpan<byte> span = Value.Span;
        if (span.IsEmpty)
        {
            return string.Empty;
        }

#if NETSTANDARD2_0
        return Latin1.GetString(span.ToArray());
#else
        return Latin1.GetString(span);
#endif
    }
}
