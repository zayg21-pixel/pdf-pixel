using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// An <see cref="IPdfTypeface"/> backed by an SFNT font program with TrueType ("glyf") outlines.
/// </summary>
public class TrueTypePdfTypeface : IPdfTypeface
{
    private static readonly (ushort PlatformId, ushort EncodingId)[] PreferredUnicodeCmapSubtables =
    [
        (3, 1), // Windows, Unicode BMP
        (3, 10), // Windows, Unicode full repertoire
        (0, 4), // Unicode full repertoire
        (0, 3), // Unicode BMP
        (0, 6) // Unicode full repertoire (variation)
    ];

    private readonly SfntFont _font;
    private readonly SfntHead _head;
    private readonly SfntMaxp _maxp;
    private readonly Dictionary<ushort, string>? _gidToUnicode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrueTypePdfTypeface"/> class by parsing and
    /// repacking an SFNT font program.
    /// </summary>
    /// <param name="fontBytes">The raw SFNT font program bytes.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public TrueTypePdfTypeface(in ReadOnlyMemory<byte> fontBytes, ILoggerFactory loggerFactory)
    {
        SfntFontProcessor processor = new(loggerFactory);
        SfntFont? font = processor.Read(fontBytes);
        if (font == null || font.Head == null || font.Maxp == null)
        {
            throw new ArgumentException("Data is not a valid SFNT font program.", nameof(fontBytes));
        }

        _font = font;
        _head = font.Head;
        _maxp = font.Maxp;
        _gidToUnicode = BuildGidToUnicode(font.Cmap);

        FontBytes = processor.Write(font);
        Metrics = BuildMetrics(font, _head);
    }

    /// <inheritdoc/>
    public PdfFontMetrics Metrics { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> FontBytes { get; }

    /// <inheritdoc/>
    public bool IsGidExists(ushort gid) => gid < _maxp.NumGlyphs;

    /// <inheritdoc/>
    public float GetWidth(ushort gid) => (_font.Hmtx != null && gid < _font.Hmtx.Metrics.Count) ? _font.Hmtx.Metrics[gid].AdvanceWidth : 0f;

    /// <inheritdoc/>
    public string? GetUnicode(ushort gid) => (_gidToUnicode != null && _gidToUnicode.TryGetValue(gid, out string? unicode)) ? unicode : null;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetPath(ushort gid)
    {
        if (_font.Glyf == null || gid >= _font.Glyf.Glyphs.Count)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        SfntGlyphCharacter? glyph = _font.Glyf.Glyphs[gid];
        return glyph?.Path ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc/>
    public ushort? GetGid(string unicode)
    {
        if (string.IsNullOrEmpty(unicode) || _font.Cmap == null)
        {
            return null;
        }

        int codepoint = char.ConvertToUtf32(unicode, 0);

        foreach ((ushort platformId, ushort encodingId) in PreferredUnicodeCmapSubtables)
        {
            foreach (SfntCmapSubtable subtable in _font.Cmap.Subtables)
            {
                if (subtable.PlatformId == platformId
                    && subtable.EncodingId == encodingId
                    && subtable.CodeToGid != null
                    && subtable.CodeToGid.TryGetValue(codepoint, out ushort gid))
                {
                    return gid;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a GID-to-Unicode reverse lookup from the first cmap subtable found in
    /// <see cref="PreferredUnicodeCmapSubtables"/> priority order - the ones whose codes are already
    /// genuine Unicode code points, unlike a Symbol or Mac Roman subtable's.
    /// </summary>
    private static Dictionary<ushort, string>? BuildGidToUnicode(SfntCmap? cmap)
    {
        if (cmap == null)
        {
            return null;
        }

        foreach ((ushort platformId, ushort encodingId) in PreferredUnicodeCmapSubtables)
        {
            foreach (SfntCmapSubtable subtable in cmap.Subtables)
            {
                if (subtable.PlatformId != platformId || subtable.EncodingId != encodingId || subtable.CodeToGid == null)
                {
                    continue;
                }

                Dictionary<ushort, string> gidToUnicode = [];
                foreach (KeyValuePair<int, ushort> pair in subtable.CodeToGid)
                {
                    if (IsValidUnicodeCodepoint(pair.Key) && !gidToUnicode.ContainsKey(pair.Value))
                    {
                        gidToUnicode[pair.Value] = char.ConvertFromUtf32(pair.Key);
                    }
                }

                return gidToUnicode;
            }
        }

        return null;
    }

    private static bool IsValidUnicodeCodepoint(int codepoint) => codepoint is >= 0 and <= 0x10FFFF && (codepoint < 0xD800 || codepoint > 0xDFFF);

    private static PdfFontMetrics BuildMetrics(SfntFont font, SfntHead head)
    {
        SfntOs2? os2 = font.Os2;
        SfntHhea? hhea = font.Hhea;
        SfntPost? post = font.Post;

        float ascent = os2?.STypoAscender ?? hhea?.Ascender ?? 0f;
        float descent = os2?.STypoDescender ?? hhea?.Descender ?? 0f;
        float italicAngle = post?.ItalicAngle ?? 0f;
        ushort fsSelection = os2?.FsSelection ?? 0;

        return new PdfFontMetrics
        {
            FontName = ResolveFontName(font.Name),
            Ascent = ascent,
            Descent = descent,
            CapHeight = os2?.SCapHeight ?? 0f,
            XHeight = os2?.SxHeight ?? 0f,
            ItalicAngle = italicAngle,
            BoundingBoxLeft = head.XMin,
            BoundingBoxBottom = head.YMin,
            BoundingBoxRight = head.XMax,
            BoundingBoxTop = head.YMax,
            AvgWidth = os2?.XAvgCharWidth ?? 0f,
            Weight = os2?.UsWeightClass ?? 400,
            IsForceBold = (fsSelection & 0x0020) != 0, // OS/2 fsSelection bit 5: BOLD
            IsItalic = italicAngle != 0f || (fsSelection & 0x0001) != 0, // OS/2 fsSelection bit 0: ITALIC
            Panose = os2?.Panose
        };
    }

    private static PdfFontString ResolveFontName(SfntName? name)
    {
        if (name == null)
        {
            return PdfFontString.Empty;
        }

        return FindWindowsNameRecord(name, nameId: 6)
            ?? FindWindowsNameRecord(name, nameId: 4)
            ?? FindWindowsNameRecord(name, nameId: 1)
            ?? PdfFontString.Empty;
    }

    /// <summary>
    /// Finds a Windows-platform (platformId 3), Unicode BMP (encodingId 1) name record - the encoding
    /// this codebase's own writer uses, and the one virtually every real font carries - and decodes
    /// its big-endian UTF-16 bytes.
    /// </summary>
    private static PdfFontString? FindWindowsNameRecord(SfntName name, ushort nameId)
    {
        foreach (SfntNameRecord record in name.Records)
        {
            if (record.NameId == nameId && record.PlatformId == 3 && record.EncodingId == 1)
            {
                return (PdfFontString)Encoding.BigEndianUnicode.GetString(record.Value.ToArray());
            }
        }

        return null;
    }
}
