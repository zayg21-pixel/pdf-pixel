using System;
using System.IO.Hashing;

namespace PdfPixel.Imaging;

/// <summary>
/// Identifies an inline image by the hash of its entries and ID operator, and by its data.
/// </summary>
internal readonly struct InlineImageKey : IEquatable<InlineImageKey>
{
    /// <summary>
    /// Initializes a new <see cref="InlineImageKey"/> from the <paramref name="header"/> an inline image is read from, its
    /// entries and ID operator, and its <paramref name="data"/>.
    /// </summary>
    public InlineImageKey(in ReadOnlySpan<byte> header, byte[] data)
    {
        HeaderHash = XxHash3.HashToUInt64(header);
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Hash of the entries and ID operator.
    /// </summary>
    public ulong HeaderHash { get; }

    /// <summary>
    /// Data of the image.
    /// </summary>
    public byte[] Data { get; }

    /// <inheritdoc/>
    public bool Equals(InlineImageKey other) => HeaderHash == other.HeaderHash && Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is InlineImageKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(HeaderHash, XxHash3.HashToUInt64(Data));

    /// <summary>
    /// Determines whether two keys identify the same inline image.
    /// </summary>
    public static bool operator ==(in InlineImageKey left, in InlineImageKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two keys identify different inline images.
    /// </summary>
    public static bool operator !=(in InlineImageKey left, in InlineImageKey right) => !left.Equals(right);
}
