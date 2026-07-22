using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.CffV2;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using System;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// An <see cref="IPdfTypeface"/> backed by a bare CFF font program.
/// </summary>
public class CffPdfTypeface : IPdfTypeface
{
    private readonly CffFont _font;
    private readonly PdfFontString[]? _gidToName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffPdfTypeface"/> class by parsing and repacking
    /// a CFF font program.
    /// </summary>
    /// <param name="fontBytes">The raw CFF font program bytes.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public CffPdfTypeface(in ReadOnlyMemory<byte> fontBytes, ILoggerFactory loggerFactory)
    {
        CffTypefaceReader reader = new(loggerFactory);
        CffTypeface? typeface = reader.Read(fontBytes);
        if (typeface == null || typeface.Fonts.Length == 0)
        {
            throw new ArgumentException("Data is not a valid CFF font program.", nameof(fontBytes));
        }

        _font = typeface.Fonts[0];
        _gidToName = BuildGidToName(_font, typeface.Strings);

        FontBytes = new CffTypefaceWriter().Write(typeface);
        Metrics = BuildMetrics(_font);
    }

    /// <inheritdoc/>
    public PdfFontMetrics Metrics { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FontBytes { get; }

    /// <inheritdoc/>
    public bool IsGidExists(ushort gid) => gid < _font.Characters.Length;

    /// <inheritdoc/>
    public float GetWidth(ushort gid) => (IsGidExists(gid)) ? _font.Characters[gid].Width : 0f;

    /// <inheritdoc/>
    public string? GetUnicode(ushort gid)
    {
        if (_gidToName == null || gid >= _gidToName.Length)
        {
            return null;
        }

        PdfFontString name = _gidToName[gid];
        return (!name.IsEmpty && AdobeGlyphList.CharacterMap.TryGetValue(name, out string? unicode)) ? unicode : null;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetPath(ushort gid) => (IsGidExists(gid)) ? _font.Characters[gid].Path : ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Always returns null. Fallback/substitution typefaces are always TrueType in this codebase, so
    /// a CFF typeface never needs a Unicode-to-GID lookup.
    /// </summary>
    public ushort? GetGid(string unicode) => null;

    /// <summary>
    /// Builds a GID-indexed glyph name table from the font's charset, for <see cref="GetUnicode"/> to
    /// look up via the Adobe Glyph List. Null for a CID-keyed font, which has no glyph names.
    /// </summary>
    private static PdfFontString[]? BuildGidToName(CffFont font, ReadOnlyMemory<byte>[] customStrings)
    {
        bool isCidFont = font.FdArray.Length > 0;
        if (isCidFont || font.Charset == null)
        {
            return null;
        }

        ushort[] sidsByGid = font.Charset.SidsByGid;
        var gidToName = new PdfFontString[sidsByGid.Length];
        for (int gid = 0; gid < sidsByGid.Length; gid++)
        {
            gidToName[gid] = ResolveName(sidsByGid[gid], customStrings);
        }

        return gidToName;
    }

    private static PdfFontString ResolveName(ushort sid, ReadOnlyMemory<byte>[] customStrings)
    {
        if (sid < CffStandardStrings.StandardStrings.Length)
        {
            return CffStandardStrings.StandardStrings[sid];
        }

        int customIndex = sid - CffStandardStrings.StandardStrings.Length;
        return ((uint)customIndex < (uint)customStrings.Length) ? customStrings[customIndex] : default;
    }

    private static PdfFontMetrics BuildMetrics(CffFont font)
    {
        float[]? fontBBox = font.Dict.TopDict.FontBBox;
        float italicAngle = font.Dict.TopDict.ItalicAngle ?? 0f;

        PdfFontMetrics metrics = new()
        {
            FontName = font.Name,
            ItalicAngle = italicAngle,
            IsItalic = italicAngle != 0f
        };

        if (fontBBox?.Length == 4)
        {
            metrics.BoundingBoxLeft = fontBBox[0];
            metrics.BoundingBoxBottom = fontBBox[1];
            metrics.BoundingBoxRight = fontBBox[2];
            metrics.BoundingBoxTop = fontBBox[3];
            metrics.Ascent = fontBBox[3];
            metrics.Descent = fontBBox[1];
        }

        return metrics;
    }
}
