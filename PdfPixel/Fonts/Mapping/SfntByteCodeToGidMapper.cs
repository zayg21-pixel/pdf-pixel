using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.TrueType;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for SFNT-based fonts (TrueType/OpenType) using SKTypeface.
/// For symbolic fonts, uses direct code-to-GID mapping from the font's own cmap as the primary method.
/// For non-symbolic fonts, resolves character codes to glyph names, then to GIDs via the font's
/// 'post' table or Unicode cmap.
/// </summary>
internal class SfntByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly PdfFontEncoding _encoding;
    private readonly Dictionary<int, PdfString> _differences;
    private readonly ushort[]? _singleByteCodeToGid;
    private readonly Dictionary<PdfString, ushort>? _nameToGid;
    private readonly Dictionary<string, ushort>? _unicodeToGid;
    private readonly SfntFontTables _d;

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

        if (!substituted && (flags & PdfFontFlags.Symbolic) != 0)
        {
            _singleByteCodeToGid = ExtractSingleByteCodeToGid(fontTables);
        }
        else
        {
            _nameToGid = fontTables.NameToGid;
            _unicodeToGid = ExtractUnicodeToGid(fontTables);
        }
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// For symbolic fonts, the code is used directly against the font's own single-byte cmap.
    /// For non-symbolic fonts, the code is resolved to a glyph name, then to a GID via the font's
    /// 'post' table or Unicode cmap.
    /// Returns 0 if the mapping is not found.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code)
    {
        if (_singleByteCodeToGid != null)
        {
            return _singleByteCodeToGid[code];
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
    /// Returns 0 for now (not yet implemented for SFNT fonts).
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph width for the character code, or 0.</returns>
    public float GetWidth(byte code) => 0;

    /// <summary>
    /// Builds a single-byte code to GID mapping from the first CMap subtable with an already-parsed
    /// code-to-GID mapping. As per PDF spec, symbol encoding is preferred, followed by MacRoman,
    /// then any other encoding as fallback.
    /// </summary>
    private static ushort[]? ExtractSingleByteCodeToGid(SfntFontTables fontTables)
    {
        if (fontTables.CMapEntries.Count == 0)
        {
            return null;
        }

        return ApplyEncoding(fontTables.CMapEntries.Where(entry => entry.Encoding == PdfFontEncoding.SymbolEncoding))
            ?? ApplyEncoding(fontTables.CMapEntries.Where(entry => entry.Encoding == PdfFontEncoding.MacRomanEncoding))
            ?? ApplyEncoding(fontTables.CMapEntries);
    }

    private static ushort[]? ApplyEncoding(IEnumerable<SfntCMapEntry> entries)
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
                result[(byte)kvp.Key] = kvp.Value;
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
