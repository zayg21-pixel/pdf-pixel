using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.CffV2;
using PdfPixel.Fonts.Model;
using System;
using System.IO;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// An <see cref="IPdfTypeface"/> backed by a bare CFF font program.
/// </summary>
public sealed class CffPdfTypeface : IPdfTypeface
{
    private readonly CffFont _font;
    private readonly Stream? _fontStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffPdfTypeface"/> class by reading a CFF font
    /// program from a stream, then parsing and repacking it. Takes ownership of <paramref name="fontStream"/>;
    /// it is disposed together with this instance.
    /// </summary>
    /// <param name="fontStream">Stream containing the raw CFF font program bytes.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public CffPdfTypeface(Stream fontStream, ILoggerFactory loggerFactory)
        : this(ParseTypeface(ReadAllBytes(fontStream), loggerFactory))
    {
        _fontStream = fontStream;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CffPdfTypeface"/> class from an already-parsed
    /// CFF typeface. Repacking into an OpenType (OTTO) container happens lazily, on every call to
    /// <see cref="GetFontStream"/>.
    /// </summary>
    /// <param name="typeface">The parsed CFF typeface. Must contain at least one font.</param>
    public CffPdfTypeface(CffTypeface typeface)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (typeface.Fonts.Length == 0)
        {
            throw new ArgumentException("Typeface has no fonts.", nameof(typeface));
        }

        CffTypeface = typeface;
        _font = typeface.Fonts[0];

        Metrics = BuildMetrics(_font);
    }

    private static byte[] ReadAllBytes(Stream fontStream)
    {
        using MemoryStream memoryStream = new();
        fontStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static CffTypeface ParseTypeface(in ReadOnlyMemory<byte> fontBytes, ILoggerFactory loggerFactory)
    {
        CffTypefaceReader reader = new(loggerFactory);
        CffTypeface? typeface = reader.Read(fontBytes);
        if (typeface == null)
        {
            throw new ArgumentException("Data is not a valid CFF font program.", nameof(fontBytes));
        }

        return typeface;
    }

    /// <summary>
    /// Gets the parsed CFF typeface data this instance wraps.
    /// </summary>
    public CffTypeface CffTypeface { get; }

    /// <inheritdoc/>
    public PdfFontMetrics Metrics { get; }

    /// <inheritdoc/>
    public Stream GetFontStream()
    {
        byte[]? fontBytes = CffOpenTypeWrapper.Wrap(CffTypeface);
        if (fontBytes == null)
        {
            throw new InvalidOperationException("Failed to wrap CFF typeface in an OpenType container.");
        }

        return new MemoryStream(fontBytes, writable: false);
    }

    /// <inheritdoc/>
    public bool IsGidExists(ushort gid) => gid < _font.Characters.Length;

    /// <inheritdoc/>
    public float GetWidth(ushort gid) => (IsGidExists(gid)) ? _font.Characters[gid].Width / Metrics.UnitsPerEm : 0f;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetPath(ushort gid) => (IsGidExists(gid)) ? _font.Characters[gid].Path : ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Always returns null. Fallback/substitution typefaces are always TrueType in this codebase, so
    /// a CFF typeface never needs a Unicode-to-GID lookup.
    /// </summary>
    public ushort? GetGid(string unicode) => null;

    private static PdfFontMetrics BuildMetrics(CffFont font)
    {
        float[]? fontBBox = font.Dict.TopDict.FontBBox;
        float italicAngle = font.Dict.TopDict.ItalicAngle ?? 0f;
        float unitsPerEm = CffTopDict.CombineFontMatrix(font.Dict.TopDict, null).UnitsPerEm;

        PdfFontMetrics metrics = new()
        {
            FontName = font.Name,
            UnitsPerEm = unitsPerEm,
            ItalicAngle = italicAngle,
            IsItalic = italicAngle != 0f
        };

        if (fontBBox?.Length == 4)
        {
            metrics.BoundingBoxLeft = fontBBox[0] / unitsPerEm;
            metrics.BoundingBoxBottom = fontBBox[1] / unitsPerEm;
            metrics.BoundingBoxRight = fontBBox[2] / unitsPerEm;
            metrics.BoundingBoxTop = fontBBox[3] / unitsPerEm;
            metrics.Ascent = fontBBox[3] / unitsPerEm;
            metrics.Descent = fontBBox[1] / unitsPerEm;
        }

        return metrics;
    }

    /// <inheritdoc/>
    public void Dispose() => _fontStream?.Dispose();
}
