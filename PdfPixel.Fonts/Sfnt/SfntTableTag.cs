using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents a 4-byte SFNT table tag (e.g. "cmap", "glyf"), stored as its big-endian UInt32 encoding.
/// </summary>
public readonly struct SfntTableTag : IEquatable<SfntTableTag>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntTableTag"/> struct.
    /// </summary>
    /// <param name="value">The big-endian UInt32 encoding of the 4-character tag.</param>
    public SfntTableTag(uint value) => Value = value;

    /// <summary>
    /// Gets the big-endian UInt32 encoding of the 4-character tag.
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// Creates a <see cref="SfntTableTag"/> from its 4-character string representation.
    /// </summary>
    /// <param name="tag">The 4-character tag, e.g. "head" or "cmap".</param>
    public static SfntTableTag FromString(string tag)
    {
        if (tag == null || tag.Length != 4)
        {
            throw new ArgumentException("Tag must be exactly 4 characters.", nameof(tag));
        }

        uint value = (uint)tag[0] << 24 | (uint)tag[1] << 16 | (uint)tag[2] << 8 | tag[3];
        return new SfntTableTag(value);
    }

    /// <inheritdoc/>
    public bool Equals(SfntTableTag other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SfntTableTag other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Equality operator for <see cref="SfntTableTag"/>.
    /// </summary>
    public static bool operator ==(in SfntTableTag left, in SfntTableTag right) => left.Equals(right);

    /// <summary>
    /// Inequality operator for <see cref="SfntTableTag"/>.
    /// </summary>
    public static bool operator !=(in SfntTableTag left, in SfntTableTag right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
    {
        var characters = new char[4];
        characters[0] = (char)((Value >> 24) & 0xFF);
        characters[1] = (char)((Value >> 16) & 0xFF);
        characters[2] = (char)((Value >> 8) & 0xFF);
        characters[3] = (char)(Value & 0xFF);
        return new string(characters);
    }
}
