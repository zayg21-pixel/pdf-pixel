using PdfPixel.Color.ColorSpace;
using System;
using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging;

/// <summary>
/// Identifies an inline image by the hash of its entries and ID operator, by the color space its /ColorSpace resolved
/// to, and by its data.
/// </summary>
internal readonly struct InlineImageKey : IEquatable<InlineImageKey>
{
    /// <summary>
    /// Initializes a new <see cref="InlineImageKey"/> from the <paramref name="header"/> an inline image is read from, its
    /// entries and ID operator, the <paramref name="colorSpace"/> its /ColorSpace resolved to, and its
    /// <paramref name="data"/>.
    /// </summary>
    public InlineImageKey(in ReadOnlySpan<byte> header, PdfColorSpaceConverter? colorSpace, byte[] data)
    {
        HeaderHash = XxHash3.HashToUInt64(header);
        ColorSpace = colorSpace;
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Hash of the entries and ID operator.
    /// </summary>
    public ulong HeaderHash { get; }

    /// <summary>
    /// Color space the image's /ColorSpace resolved to in the resources it was read with, or <see langword="null"/> for
    /// a stencil mask.
    /// </summary>
    public PdfColorSpaceConverter? ColorSpace { get; }

    /// <summary>
    /// Data of the image.
    /// </summary>
    public byte[] Data { get; }

    /// <inheritdoc/>
    public bool Equals(InlineImageKey other)
    {
        return HeaderHash == other.HeaderHash
            && ReferenceEquals(ColorSpace, other.ColorSpace)
            && Data.AsSpan().SequenceEqual(other.Data);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is InlineImageKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int colorSpaceHash = 0;

        if (ColorSpace != null)
        {
            colorSpaceHash = RuntimeHelpers.GetHashCode(ColorSpace);
        }

        return HashCode.Combine(HeaderHash, colorSpaceHash, XxHash3.HashToUInt64(Data));
    }

    /// <summary>
    /// Determines whether two keys identify the same inline image.
    /// </summary>
    public static bool operator ==(in InlineImageKey left, in InlineImageKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two keys identify different inline images.
    /// </summary>
    public static bool operator !=(in InlineImageKey left, in InlineImageKey right) => !left.Equals(right);
}
