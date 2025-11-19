using System;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Immutable representation of a PDF character code bytes.
/// Holds the original byte slice as ReadOnlyMemory<byte> and compares by byte sequence.
/// This is length-aware by design (e.g., 0x41 != 0x00 0x41).
/// </summary>
public sealed class PdfCharacterCode : IEquatable<PdfCharacterCode>
{
    /// <summary>
    /// Create a new Character code from a read-only byte slice. The data is not copied.
    /// </summary>
    public PdfCharacterCode(ReadOnlyMemory<byte> bytes)
    {
        Bytes = bytes;
    }

    /// <summary>
    /// Underlying byte sequence for this character code (CID/code bytes).
    /// </summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>
    /// Number of bytes in this character code.
    /// </summary>
    public int Length => Bytes.Length;

    public bool Equals(PdfCharacterCode other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Bytes.Span.SequenceEqual(other.Bytes.Span);
    }

    public override bool Equals(object obj) => obj is PdfCharacterCode other && Equals(other);

    public override int GetHashCode()
    {
        // FNV-1a over bytes plus length to reduce collisions for mixed-length codes
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffset;
            var span = Bytes.Span;
            for (int i = 0; i < span.Length; i++)
            {
                hash ^= span[i];
                hash *= fnvPrime;
            }
            hash ^= (uint)span.Length;
            return (int)hash;
        }
    }

    public static bool operator ==(PdfCharacterCode left, PdfCharacterCode right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(PdfCharacterCode left, PdfCharacterCode right) => !(left == right);

    /// <summary>
    /// For diagnostics: returns a hex string like "41" or "00-41".
    /// </summary>
    public override string ToString()
    {
        var span = Bytes.Span;
        if (span.Length == 0) return string.Empty;
        char[] chars = new char[span.Length * 3 - 1];
        int idx = 0;
        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            chars[idx++] = GetHexNibble(b >> 4 & 0x0F);
            chars[idx++] = GetHexNibble(b & 0x0F);
            if (i < span.Length - 1) chars[idx++] = '-';
        }
        return new string(chars);
    }

    private static char GetHexNibble(int v) => (char)(v < 10 ? '0' + v : 'A' + (v - 10));

    /// <summary>
    /// Pack a uint into minimal-length big-endian bytes (no leading zero bytes).
    /// </summary>
    public static ReadOnlyMemory<byte> PackUIntToMinimalBigEndian(uint value)
    {
        if (value == 0u)
        {
            // Represent zero as a single 0x00 byte
            return new byte[] { 0x00 };
        }

        // Build bytes from least significant to most, then slice
        byte[] tmp = new byte[4];
        int index = 4;
        while (value != 0u)
        {
            tmp[--index] = (byte)(value & 0xFF);
            value >>= 8;
        }
        int len = 4 - index;
        var result = new byte[len];
        Buffer.BlockCopy(tmp, index, result, 0, len);
        return result;
    }

    /// <summary>
    /// Implicit conversion from uint to character code.
    /// FAST-PATH COMPAT: Packs the integer value into a minimal-length big-endian byte sequence.
    /// LIMITATION: Leading zero bytes are not preserved (e.g., 0x0041 becomes [0x41]).
    /// This is intended for transitional compatibility with existing uint-based code.
    /// </summary>
    public static implicit operator PdfCharacterCode(uint value)
    {
        return new PdfCharacterCode(PackUIntToMinimalBigEndian(value));
    }

    /// <summary>
    /// Implicit conversion from character code to uint.
    /// Interprets the code bytes as big-endian and returns the numeric value.
    /// </summary>
    public static implicit operator uint(PdfCharacterCode code)
    {
        if (code is null || code.Bytes.Length == 0)
        {
            return 0u;
        }
        return UnpackBigEndianToUInt(code.Bytes.Span);
    }


    /// <summary>
    /// Pack a uint into exactly 'length' big-endian bytes (1..4), padding with leading zeros if needed.
    /// </summary>
    public static ReadOnlyMemory<byte> PackUIntToBigEndian(uint value, int length)
    {
        if (length <= 0)
        {
            return PackUIntToMinimalBigEndian(value);
        }
        if (length > 4)
        {
            length = 4;
        }
        var bytes = new byte[length];
        for (int i = length - 1; i >= 0; i--)
        {
            bytes[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return bytes;
    }

    /// <summary>
    /// Interpret a big-endian byte sequence as an unsigned integer (up to 4 bytes).
    /// </summary>
    public static uint UnpackBigEndianToUInt(ReadOnlySpan<byte> span)
    {
        uint v = 0u;
        for (int i = 0; i < span.Length; i++)
        {
            v = v << 8 | span[i];
        }
        return v;
    }
}
