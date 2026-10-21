using System;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Immutable representation of a PDF character code of 1 to 4 bytes.
/// This is length-aware by design (e.g., 0x41 != 0x00 0x41).
/// </summary>
public readonly struct PdfCharacterCode : IEquatable<PdfCharacterCode>
{
    private const int MaxLength = sizeof(uint);

    /// <summary>
    /// Creates a new character code from its big-endian bytes; of a longer sequence the last 4 bytes are kept.
    /// </summary>
    public PdfCharacterCode(in ReadOnlySpan<byte> bytes)
    {
        Value = UnpackBigEndianToUInt(bytes);
        Length = Math.Min(bytes.Length, MaxLength);
    }

    /// <summary>
    /// Creates a new character code from its numeric value and byte count.
    /// </summary>
    public PdfCharacterCode(uint value, int length)
    {
        Value = value;
        Length = length;
    }

    /// <summary>
    /// Numeric value of the code bytes, read as big-endian.
    /// </summary>
    public uint Value { get; }

    /// <summary>
    /// Number of bytes in this character code.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc/>
    public bool Equals(PdfCharacterCode other) => Value == other.Value && Length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfCharacterCode other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Value, Length);

    /// <inheritdoc/>
    public static bool operator ==(in PdfCharacterCode left, in PdfCharacterCode right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(in PdfCharacterCode left, in PdfCharacterCode right) => !left.Equals(right);

    /// <summary>
    /// For diagnostics: returns a hex string like "41" or "00-41".
    /// </summary>
    public override string ToString()
    {
        if (Length == 0)
        {
            return string.Empty;
        }

        var chars = new char[(Length * 3) - 1];
        int idx = 0;
        for (int i = 0; i < Length; i++)
        {
            var b = (byte)(Value >> ((Length - 1 - i) * 8));
            chars[idx++] = GetHexNibble(b >> 4 & 0x0F);
            chars[idx++] = GetHexNibble(b & 0x0F);
            if (i < Length - 1)
            {
                chars[idx++] = '-';
            }
        }

        return new string(chars);
    }

    private static char GetHexNibble(int v) => (char)((v < 10) ? '0' + v : 'A' + (v - 10));

    private static uint UnpackBigEndianToUInt(in ReadOnlySpan<byte> span)
    {
        uint v = 0u;
        for (int i = 0; i < span.Length; i++)
        {
            v = v << 8 | span[i];
        }

        return v;
    }
}
