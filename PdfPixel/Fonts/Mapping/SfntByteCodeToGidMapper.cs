using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Typeface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for SFNT-based fonts (TrueType/OpenType).
/// </summary>
internal class SfntByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly ushort[] _codeToGid = new ushort[256];
    private readonly float?[] _codeToWidth = new float?[256];

    /// <summary>
    /// Initializes a new instance of <see cref="SfntByteCodeToGidMapper"/> for the specified typeface and encoding.
    /// </summary>
    /// <param name="typeface">The typeface whose font to map. Cmap lookups are resolved through it.</param>
    /// <param name="flags">Flags defined in PDF font.</param>
    /// <param name="substituted">Indicates if the font is substituted.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    public SfntByteCodeToGidMapper(
        SfntPdfTypeface typeface,
        PdfFontFlags flags,
        bool substituted,
        PdfFontEncodingInfo encodingInfo)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        SfntFont font = typeface.SfntFont;

        PdfFontEncoding encoding = encodingInfo.BaseEncoding.ToPdfFontEncoding();
        bool hasEncoding = !(encoding == PdfFontEncoding.Unknown && encodingInfo.Differences.Count == 0);

        IReadOnlyList<SfntCmapSubtable> cmapSubtables = font.Cmap?.Subtables ?? [];

        ushort[]? singleByteCodeToGid = null;
        if (!substituted && (flags & PdfFontFlags.Symbolic) != 0)
        {
            singleByteCodeToGid = ExtractSingleByteCodeToGid(typeface, cmapSubtables, hasEncoding);
        }

        IReadOnlyDictionary<PdfFontString, ushort> nameToGid = font.Post?.NameToGid ?? new Dictionary<PdfFontString, ushort>();
        List<SfntCmapSubtable> orderedCmapSubtables = cmapSubtables.OrderBy(GetCMapPriority).ToList();

        for (int code = 0; code < 256; code++)
        {
            ushort gid = ResolveGid(typeface, (byte)code, singleByteCodeToGid, encoding, encodingInfo, nameToGid, orderedCmapSubtables);
            _codeToGid[code] = gid;

            if (gid != 0)
            {
                _codeToWidth[code] = typeface.GetWidth(gid);
            }
        }
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code) => _codeToGid[code];

    /// <summary>
    /// Gets the glyph width for the specified character code.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph width for the character code, or <see langword="null"/> if not found.</returns>
    public float? GetWidth(byte code) => _codeToWidth[code];

    private static ushort ResolveGid(
        SfntPdfTypeface typeface,
        byte code,
        ushort[]? singleByteCodeToGid,
        PdfFontEncoding encoding,
        PdfFontEncodingInfo encodingInfo,
        IReadOnlyDictionary<PdfFontString, ushort> nameToGid,
        IReadOnlyList<SfntCmapSubtable> orderedCmapSubtables)
    {
        if (singleByteCodeToGid != null)
        {
            ushort gid = singleByteCodeToGid[code];

            if (gid != 0)
            {
                return gid;
            }
        }

        PdfFontString name = encodingInfo.GetNameByCode(code);

        if (name.IsEmpty)
        {
            return 0;
        }

        if (nameToGid.TryGetValue(name, out ushort gidByName))
        {
            return gidByName;
        }

        if (AdobeGlyphList.TryGetUnicode(encoding, name, out string? unicode)
            && unicode != null
            && TryGetGidByUnicode(typeface, orderedCmapSubtables, unicode, out ushort gidByUnicode))
        {
            return gidByUnicode;
        }

        return 0;
    }

    private static ushort[]? ExtractSingleByteCodeToGid(SfntPdfTypeface typeface, IReadOnlyList<SfntCmapSubtable> cmapSubtables, bool hasEncoding)
    {
        if (cmapSubtables.Count == 0)
        {
            return null;
        }

        ushort[]? result = ApplyEncoding(typeface, cmapSubtables.Where(subtable => subtable.Encoding == PdfFontEncoding.SymbolEncoding), PdfFontEncoding.SymbolEncoding);

        // Heuristic: a Symbolic font is not supposed to declare an Encoding, looks like 1 particular generator creates ANSI symbolic encoding
        if (result == null && hasEncoding)
        {
            result = ApplyEncoding(typeface, cmapSubtables.Where(subtable => subtable.Encoding == PdfFontEncoding.WinAnsiEncoding), PdfFontEncoding.WinAnsiEncoding);
        }

        if (result == null)
        {
            result = ApplyEncoding(typeface, cmapSubtables.Where(subtable => subtable.Encoding == PdfFontEncoding.MacRomanEncoding), PdfFontEncoding.MacRomanEncoding);
        }

        // Some subsetted symbolic fonts ship only a platform 0 (Unicode) or WinAnsi-tagged (3,1)
        // subtable with no named PDF encoding; its codes are used directly as raw single-byte
        // character codes rather than being discarded for lack of a matching PDF encoding.
        return result ?? ApplyEncoding(typeface, cmapSubtables.Where(subtable => subtable.PlatformId == 0 || subtable.Encoding == PdfFontEncoding.WinAnsiEncoding), PdfFontEncoding.Unknown);
    }

    /// <summary>
    /// Builds a single-byte code-to-GID array by querying each matching subtable directly for codes
    /// 0-255, rather than enumerating its full contents. For Symbol (3,0) subtables, both the raw code
    /// and its 0xF000-0xF0FF-wrapped form (per the PDF spec's symbolic TrueType convention, 9.6.6.4)
    /// are tried, the wrapped form taking priority - matching a Symbol cmap's usual encoding.
    /// If more than one subtable matches <paramref name="tag"/>, the last one with any mapping wins.
    /// </summary>
    private static ushort[]? ApplyEncoding(SfntPdfTypeface typeface, IEnumerable<SfntCmapSubtable> subtables, PdfFontEncoding tag)
    {
        ushort[]? result = null;
        foreach (SfntCmapSubtable subtable in subtables)
        {
            var candidate = new ushort[256];
            var hasAnyMapping = false;

            for (int code = 0; code < 256; code++)
            {
                ushort? gid;
                if (tag == PdfFontEncoding.SymbolEncoding)
                {
                    gid = typeface.GetGid(subtable, 0xF000 | code) ?? typeface.GetGid(subtable, code);
                }
                else
                {
                    gid = typeface.GetGid(subtable, code);
                }

                if (gid != null)
                {
                    candidate[code] = gid.Value;
                    hasAnyMapping = true;
                }
            }

            if (hasAnyMapping)
            {
                result = candidate;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a Unicode string to a glyph id by querying <paramref name="orderedCmapSubtables"/> in
    /// priority order (see <see cref="GetCMapPriority"/>), returning the first match. A subtable whose
    /// own codes are already Unicode (WinAnsi-tagged, or untagged) is queried directly; any other
    /// subtable's native code space isn't Unicode, so its up-to-256 codes are searched for one whose
    /// converted Unicode value matches, mirroring formats 0/6/10's 256-or-fewer-entry cost.
    /// </summary>
    private static bool TryGetGidByUnicode(SfntPdfTypeface typeface, IReadOnlyList<SfntCmapSubtable> orderedCmapSubtables, string unicode, out ushort gid)
    {
        int codepoint = char.ConvertToUtf32(unicode, 0);

        foreach (SfntCmapSubtable subtable in orderedCmapSubtables)
        {
            ushort? candidateGid;
            if (IsUnicodeNative(subtable.Encoding))
            {
                candidateGid = typeface.GetGid(subtable, codepoint);
            }
            else
            {
                candidateGid = FindGidByNativeCode(typeface, subtable, codepoint);
            }

            if (candidateGid != null)
            {
                gid = candidateGid.Value;
                return true;
            }
        }

        gid = 0;
        return false;
    }

    private static bool IsUnicodeNative(PdfFontEncoding? encoding)
        => encoding == null || encoding == PdfFontEncoding.WinAnsiEncoding || encoding == PdfFontEncoding.Unknown;

    private static ushort? FindGidByNativeCode(SfntPdfTypeface typeface, SfntCmapSubtable subtable, int targetCodepoint)
    {
        PdfFontEncoding encoding = subtable.Encoding ?? PdfFontEncoding.Unknown;

        for (int rawCode = 0; rawCode <= 255; rawCode++)
        {
            if (ConvertToUnicode(rawCode, encoding) != targetCodepoint)
            {
                continue;
            }

            ushort? gid = typeface.GetGid(subtable, rawCode);
            if (gid != null)
            {
                return gid;
            }
        }

        return null;
    }

    private static int GetCMapPriority(SfntCmapSubtable subtable)
    {
        return subtable.Encoding switch
        {
            PdfFontEncoding.WinAnsiEncoding => 0,
            PdfFontEncoding.SymbolEncoding => 1,
            PdfFontEncoding.MacRomanEncoding => 2,
            _ => 3
        };
    }

    private static int ConvertToUnicode(int code, PdfFontEncoding? encoding)
    {
        if (encoding == null
            || encoding == PdfFontEncoding.WinAnsiEncoding
            || encoding == PdfFontEncoding.Unknown)
        {
            return code;
        }

        if (code < 0 || code > 255)
        {
            return code;
        }

        PdfFontString glyphName = SingleByteEncodings.GetNameByCode((byte)code, encoding.Value);

        if (glyphName.IsEmpty)
        {
            return code;
        }

        if (AdobeGlyphList.TryGetUnicode(encoding.Value, glyphName, out string? unicode)
            && unicode != null
            && unicode.Length > 0)
        {
            return char.ConvertToUtf32(unicode, 0);
        }

        return code;
    }
}
