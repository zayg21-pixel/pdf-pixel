using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.TrueType;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for SFNT-based fonts (TrueType/OpenType) using SKTypeface.
/// </summary>
internal class SfntByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly PdfFontEncoding _encoding;
    private readonly Dictionary<int, PdfString> _differences;
    private readonly ushort[]? _singleByteCodeToGid;
    private readonly Dictionary<PdfString, ushort>? _nameToGid;
    private readonly Dictionary<string, ushort>? _unicodeToGid;
    private readonly float[]? _gidWidths;

    /// <summary>
    /// Initializes a new instance of <see cref="SfntByteCodeToGidMapper"/> for the specified font tables and encoding.
    /// </summary>
    /// <param name="fontTables">The font's parsed cmap subtables and 'post' table glyph names.</param>
    /// <param name="flags">Flags defined in PDF font.</param>
    /// <param name="substituted">Indicates if the font is substituted.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    public SfntByteCodeToGidMapper(
        SfntFontTables fontTables,
        PdfFontFlags flags,
        bool substituted,
        PdfFontEncodingInfo encodingInfo)
    {
        if (fontTables == null)
        {
            throw new ArgumentNullException(nameof(fontTables));
        }

        _encoding = encodingInfo.BaseEncoding;
        _differences = encodingInfo.Differences;

        // Heuristic: a Symbolic font (PDF spec Table 123) is not supposed to declare an Encoding.
        // When it does anyway, the flag is treated as unreliable and the font as non-symbolic.
        bool hasEncoding = !(encodingInfo.BaseEncoding == PdfFontEncoding.Unknown && encodingInfo.Differences.Count == 0);

        if (!hasEncoding && !substituted && (flags & PdfFontFlags.Symbolic) != 0)
        {
            _singleByteCodeToGid = ExtractSingleByteCodeToGid(fontTables);
        }

        _nameToGid = fontTables.NameToGid;
        _unicodeToGid = ExtractUnicodeToGid(fontTables);
        _gidWidths = fontTables.GidWidths;
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code)
    {
        if (_singleByteCodeToGid != null)
        {
            ushort gid = _singleByteCodeToGid[code];

            if (gid != 0)
            {
                return gid;
            }
        }

        PdfString name = SingleByteEncodings.GetNameByCode(code, _encoding, _differences);

        if (name.IsEmpty)
        {
            return 0;
        }

        if (_nameToGid?.TryGetValue(name, out ushort gidByName) == true)
        {
            return gidByName;
        }

        if (AdobeGlyphList.GetMap(_encoding).TryGetValue(name, out string? unicode)
            && _unicodeToGid?.TryGetValue(unicode, out ushort gidByUnicode) == true)
        {
            return gidByUnicode;
        }

        return 0;
    }

    /// <summary>
    /// Gets the glyph width for the specified character code.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph width for the character code, or 0.</returns>
    public float GetWidth(byte code)
    {
        if (_gidWidths == null || _gidWidths.Length == 0)
        {
            return 0;
        }

        ushort gid = GetGid(code);
        if (gid == 0)
        {
            return 0;
        }

        int index = Math.Min(gid, _gidWidths.Length - 1);
        return _gidWidths[index];
    }

    private static ushort[]? ExtractSingleByteCodeToGid(SfntFontTables fontTables)
    {
        if (fontTables.CMapEntries.Count == 0)
        {
            return null;
        }

        return ApplyEncoding(fontTables.CMapEntries.Where(entry => entry.Encoding == PdfFontEncoding.SymbolEncoding), PdfFontEncoding.SymbolEncoding)
            ?? ApplyEncoding(fontTables.CMapEntries.Where(entry => entry.Encoding == PdfFontEncoding.MacRomanEncoding), PdfFontEncoding.MacRomanEncoding);
    }

    /// <summary>
    /// Builds a single-byte code-to-GID array from the given cmap subtables.
    /// Codes outside 0-255 are dropped rather than truncated, to avoid collisions from a (3,1)
    /// cmap's full-range Unicode entries. For Symbol (3,0) subtables, codes 0xF000-0xF0FF are
    /// unwrapped to their low byte per the PDF spec's symbolic TrueType convention (9.6.6.4).
    /// </summary>
    private static ushort[]? ApplyEncoding(IEnumerable<SfntCMapEntry> entries, PdfFontEncoding tag)
    {
        ushort[]? result = default;
        foreach (SfntCMapEntry entry in entries)
        {
            if (entry.CodeToGid == null)
            {
                continue;
            }

            result = new ushort[256];
            foreach (KeyValuePair<int, ushort> kvp in entry.CodeToGid)
            {
                int code = kvp.Key;

                if (tag == PdfFontEncoding.SymbolEncoding && code >= 0xF000 && code <= 0xF0FF)
                {
                    code &= 0xFF;
                }

                if (code < 0 || code > 255)
                {
                    continue;
                }

                result[code] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a mapping from Unicode codepoints to glyph IDs (GIDs) using CMap formats 0, 4 and 6.
    /// </summary>
    private static Dictionary<string, ushort> ExtractUnicodeToGid(SfntFontTables fontTables)
    {
        Dictionary<string, ushort> unicodeToGid = [];

        foreach (SfntCMapEntry cmap in fontTables.CMapEntries)
        {
            if (cmap.CodeToGid == null)
            {
                continue;
            }

            foreach (KeyValuePair<int, ushort> kvp in cmap.CodeToGid)
            {
                int unicodeCodepoint = ConvertToUnicode(kvp.Key, cmap.Encoding);

                if (!IsValidUnicodeCodepoint(unicodeCodepoint))
                {
                    continue;
                }

                string unicodeString = char.ConvertFromUtf32(unicodeCodepoint);

                if (!unicodeToGid.ContainsKey(unicodeString))
                {
                    unicodeToGid[unicodeString] = kvp.Value;
                }
            }
        }

        // TODO: [MEDIUM] Add support for format 10/12
        return unicodeToGid;
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

        PdfString glyphName = SingleByteEncodings.GetNameByCode((byte)code, encoding.Value);

        if (glyphName.IsEmpty)
        {
            return code;
        }

        if (AdobeGlyphList.GetMap(encoding.Value).TryGetValue(glyphName, out string? unicode)
            && unicode != null
            && unicode.Length > 0)
        {
            return char.ConvertToUtf32(unicode, 0);
        }

        return code;
    }

    private static bool IsValidUnicodeCodepoint(int codepoint) => codepoint >= 0 && codepoint <= 0x10FFFF && (codepoint < 0xD800 || codepoint > 0xDFFF);
}
