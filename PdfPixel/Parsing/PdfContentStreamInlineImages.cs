using PdfPixel.Imaging;
using System;
using System.Collections.Generic;
using System.IO.Hashing;

namespace PdfPixel.Parsing;

/// <summary>
/// Inline images read from the content streams of a document, kept with a copy of the bytes each was read from: its
/// entries, ID operator and data. An inline image whose bytes repeat, in the same content stream or in another one such as
/// a form or the glyph procedure of a Type3 font, is read once and shared.
/// </summary>
internal sealed class PdfContentStreamInlineImages
{
    /// <summary>
    /// Number of leading bytes the images are looked up by. <see cref="Find"/> finds only images read from at least as many bytes.
    /// </summary>
    public const int PrefixLength = 64;

    private readonly Dictionary<ulong, InlineImageSource> _sources = [];

    /// <summary>
    /// Returns the image read from bytes equal to those <paramref name="content"/> starts with, with the length of those bytes,
    /// or null when none was.
    /// </summary>
    /// <param name="content">The content stream from where an inline image starts, after its BI operator.</param>
    /// <param name="length">The length of the bytes the image was read from.</param>
    public PdfImage? Find(in ReadOnlySpan<byte> content, out int length)
    {
        length = 0;

        if (content.Length < PrefixLength || !_sources.TryGetValue(HashPrefix(content), out InlineImageSource? source))
        {
            return null;
        }

        for (; source != null; source = source.Next)
        {
            if (content.StartsWith(source.Bytes))
            {
                length = source.Bytes.Length;

                return source.Image;
            }
        }

        return null;
    }

    private PdfImage? Get(in ReadOnlySpan<byte> bytes)
    {
        if (!_sources.TryGetValue(HashPrefix(bytes), out InlineImageSource? source))
        {
            return null;
        }

        for (; source != null; source = source.Next)
        {
            if (bytes.SequenceEqual(source.Bytes))
            {
                return source.Image;
            }
        }

        return null;
    }

    /// <summary>
    /// Keeps <paramref name="image"/> as read from <paramref name="bytes"/>, unless an image read from equal bytes is kept already.
    /// </summary>
    public void Add(in ReadOnlySpan<byte> bytes, PdfImage image)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (Get(bytes) != null)
        {
            return;
        }

        ulong prefixHash = HashPrefix(bytes);
        _sources.TryGetValue(prefixHash, out InlineImageSource? next);
        _sources[prefixHash] = new InlineImageSource(bytes.ToArray(), image, next);
    }

    /// <summary>
    /// Drops every kept image.
    /// </summary>
    public void Clear() => _sources.Clear();

    private static ulong HashPrefix(in ReadOnlySpan<byte> bytes) => XxHash3.HashToUInt64(bytes.Slice(0, Math.Min(PrefixLength, bytes.Length)));

    /// <summary>
    /// Bytes an inline image was read from, chained to the other sources whose prefix hashes the same.
    /// </summary>
    private sealed class InlineImageSource(byte[] bytes, PdfImage image, InlineImageSource? next)
    {
        public byte[] Bytes { get; } = bytes;

        public PdfImage Image { get; } = image;

        public InlineImageSource? Next { get; } = next;
    }
}
