using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Sfnt;
using System;
using System.IO;
using System.Text;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// An <see cref="IPdfTypeface"/> backed by an SFNT font program, with either TrueType ("glyf") or
/// CFF-flavored (OpenType "CFF ") outlines.
/// </summary>
public sealed class SfntPdfTypeface : IPdfTypeface
{
    private static readonly (ushort PlatformId, ushort EncodingId)[] PreferredUnicodeCmapSubtables =
    [
        (3, 1), // Windows, Unicode BMP
        (3, 10), // Windows, Unicode full repertoire
        (0, 4), // Unicode full repertoire
        (0, 3), // Unicode BMP
        (0, 6), // Unicode full repertoire (variation)
        (0, 2), // Unicode, ISO/IEC 10646 semantics
        (0, 1), // Unicode, Unicode 1.1 semantics
        (0, 0) // Unicode, Unicode 1.0 semantics
    ];

    private readonly SfntFont _font;
    private readonly SfntHead _head;
    private readonly SfntMaxp _maxp;
    private readonly CffFont? _cffFont;
    private readonly ReadOnlyFontStream _fontStream;
    private readonly SfntFontProcessor _processor;
    private readonly SfntPdfTypefaceParameters _parameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntPdfTypeface"/> class by reading an SFNT
    /// font program from a stream, then parsing it, using <see cref="SfntPdfTypefaceParameters.Default"/>.
    /// Takes ownership of <paramref name="fontStream"/>; it is disposed together with this instance.
    /// </summary>
    /// <param name="fontStream">Stream containing the raw SFNT font program bytes.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SfntPdfTypeface(Stream fontStream, ILoggerFactory loggerFactory)
        : this(fontStream, loggerFactory, SfntPdfTypefaceParameters.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntPdfTypeface"/> class by reading an SFNT
    /// font program from a stream, then parsing it. Takes ownership of <paramref name="fontStream"/>;
    /// it is disposed together with this instance.
    /// </summary>
    /// <param name="fontStream">Stream containing the raw SFNT font program bytes.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    /// <param name="parameters">Construction parameters.</param>
    public SfntPdfTypeface(Stream fontStream, ILoggerFactory loggerFactory, SfntPdfTypefaceParameters parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _fontStream = ReadOnlyFontStream.Create(fontStream, leaveOpen: false);
        _processor = new SfntFontProcessor(loggerFactory);
        _parameters = parameters;

        SfntFont? font = _processor.Read(_fontStream, parameters.TtcIndex);
        if (font == null || font.Head == null || font.Maxp == null)
        {
            throw new ArgumentException("Data is not a valid SFNT font program.", nameof(fontStream));
        }

        _font = font;
        _head = font.Head;
        _maxp = font.Maxp;

        if (font.CffTypeface?.Fonts.Length > 0)
        {
            _cffFont = font.CffTypeface.Fonts[0];
        }

        Metrics = BuildMetrics(font, _head);
        IsSymbolEncoded = DetectSymbolEncoding(font);
    }

    /// <summary>
    /// Gets the parsed SFNT font data this instance wraps.
    /// </summary>
    public SfntFont SfntFont => _font;

    /// <summary>
    /// <see langword="true"/> when this typeface addresses its glyphs only through a Windows Symbol
    /// (3, 0) "cmap" subtable, with no Unicode subtable at all. Its glyphs are selected by character
    /// code through <see cref="GetGidByCode"/>; a Unicode value carries no meaning for it.
    /// </summary>
    public bool IsSymbolEncoded { get; }

    /// <inheritdoc/>
    public PdfFontMetrics Metrics { get; }

    /// <inheritdoc/>
    public Stream GetFontStream()
    {
        if (_parameters.RepackTypeface)
        {
            byte[] repackedFontBytes = _processor.Write(_font, _fontStream);
            return new MemoryStream(repackedFontBytes, writable: false);
        }

        return ReadOnlyFontStream.Create(_fontStream, leaveOpen: true);
    }

    /// <inheritdoc/>
    public bool IsGidExists(ushort gid) => (_cffFont != null) ? gid < _cffFont.Characters.Length : gid < _maxp.NumGlyphs;

    /// <inheritdoc/>
    public float? GetWidth(ushort gid) => (_font.Hmtx != null && gid < _font.Hmtx.Metrics.Count) ? _font.Hmtx.Metrics[gid].AdvanceWidth / (float)_head.UnitsPerEm : null;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetPath(ushort gid)
    {
        if (_cffFont != null)
        {
            if (gid >= _cffFont.Characters.Length)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return _cffFont.Characters[gid].Path;
        }

        float unitsPerEmScale = 1f / _head.UnitsPerEm;
        PdfFontMatrix matrix = new(unitsPerEmScale, 0, 0, 0, unitsPerEmScale, 0);

        return _processor.ResolvePath(_font, gid, _fontStream, matrix);
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
                if (subtable.PlatformId != platformId || subtable.EncodingId != encodingId)
                {
                    continue;
                }

                ushort? gid = GetGid(subtable, codepoint);
                if (gid != null)
                {
                    return gid;
                }
            }
        }

        // A symbol font (e.g. Wingdings) has no genuine Unicode cmap subtable; its only subtable is
        // Windows Symbol (3, 0). Tried last, after every genuine Unicode subtable has failed.
        return GetGidByCode(codepoint);
    }

    /// <summary>
    /// Gets the glyph id this typeface's Windows Symbol (3, 0) "cmap" subtable maps the given character
    /// code to, or <see langword="null"/> when it has no such subtable or the subtable does not map the
    /// code. Both the 0xF000-0xF0FF wrapped form the PDF specification's symbolic TrueType convention
    /// (9.6.6.4) prescribes and the bare code are tried, the wrapped form first.
    /// </summary>
    /// <param name="code">The character code to look up.</param>
    public ushort? GetGidByCode(int code)
    {
        if (_font.Cmap == null)
        {
            return null;
        }

        foreach (SfntCmapSubtable subtable in _font.Cmap.Subtables)
        {
            if (subtable.Encoding != PdfFontEncoding.SymbolEncoding)
            {
                continue;
            }

            ushort? gid = GetGid(subtable, 0xF000 | code) ?? GetGid(subtable, code);
            if (gid != null)
            {
                return gid;
            }
        }

        return null;
    }

    /// <summary>
    /// Reports whether <paramref name="font"/>'s "cmap" offers a Windows Symbol (3, 0) subtable and no
    /// Unicode subtable at all.
    /// </summary>
    private static bool DetectSymbolEncoding(SfntFont font)
    {
        if (font.Cmap == null)
        {
            return false;
        }

        var hasSymbolSubtable = false;

        foreach (SfntCmapSubtable subtable in font.Cmap.Subtables)
        {
            foreach ((ushort platformId, ushort encodingId) in PreferredUnicodeCmapSubtables)
            {
                if (subtable.PlatformId == platformId && subtable.EncodingId == encodingId)
                {
                    return false;
                }
            }

            if (subtable.Encoding == PdfFontEncoding.SymbolEncoding)
            {
                hasSymbolSubtable = true;
            }
        }

        return hasSymbolSubtable;
    }

    /// <summary>
    /// Resolves a glyph id for <paramref name="code"/> from <paramref name="subtable"/>, resolving the
    /// subtable's ranges on first use.
    /// </summary>
    /// <param name="subtable">The cmap subtable to query. Must belong to <see cref="SfntFont"/>.</param>
    /// <param name="code">The subtable-native code to look up.</param>
    public ushort? GetGid(SfntCmapSubtable subtable, int code) => _processor.GetCmapGid(_font, subtable, code, _fontStream);

    private static PdfFontMetrics BuildMetrics(SfntFont font, SfntHead head)
    {
        SfntOs2? os2 = font.Os2;
        SfntHhea? hhea = font.Hhea;
        SfntPost? post = font.Post;

        float unitsPerEm = head.UnitsPerEm;
        float ascent = os2?.STypoAscender ?? hhea?.Ascender ?? 0f;
        float descent = os2?.STypoDescender ?? hhea?.Descender ?? 0f;
        float italicAngle = post?.ItalicAngle ?? 0f;
        ushort fsSelection = os2?.FsSelection ?? 0;

        return new PdfFontMetrics
        {
            FontName = ResolveFontName(font.Name),
            UnitsPerEm = unitsPerEm,
            Ascent = ascent / unitsPerEm,
            Descent = descent / unitsPerEm,
            CapHeight = (os2?.SCapHeight ?? 0f) / unitsPerEm,
            XHeight = (os2?.SxHeight ?? 0f) / unitsPerEm,
            ItalicAngle = italicAngle,
            BoundingBoxLeft = head.XMin / unitsPerEm,
            BoundingBoxBottom = head.YMin / unitsPerEm,
            BoundingBoxRight = head.XMax / unitsPerEm,
            BoundingBoxTop = head.YMax / unitsPerEm,
            AvgWidth = (os2?.XAvgCharWidth ?? 0f) / unitsPerEm,
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

    /// <inheritdoc/>
    public void Dispose() => _fontStream.Dispose();
}
