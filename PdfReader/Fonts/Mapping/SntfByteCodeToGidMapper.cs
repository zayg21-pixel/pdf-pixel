using PdfReader.Fonts.TrueType;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Text;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for SFNT-based fonts (TrueType/OpenType) using SKTypeface.
/// For single-byte fonts, uses direct code-to-GID mapping (Format 0 CMap) as the primary method.
/// Name-to-GID and Unicode-to-GID are used only as fallbacks if direct mapping is unavailable.
/// </summary>
internal class SntfByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly SntfFontTables _sntfTables;
    private readonly PdfFontFlags _flags;
    private readonly PdfFontEncoding _encoding;
    private readonly Dictionary<int, PdfString> _differences;
    private readonly PdfCMap _toUnicodeCMap;
    private readonly bool _isSubstituted;

    /// <summary>
    /// Initializes a new instance of <see cref="SntfByteCodeToGidMapper"/> for the specified typeface and encoding.
    /// </summary>
    /// <param name="fontTables">The mapped font tables.</param>
    /// <param name="flags">Flags defined in PDF font.</param>
    /// <param name="substituted">Indicates if the font is substituted.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    /// <param name="toUnicodeCMap">ToUnicode CMap for character mapping.</param>
    public SntfByteCodeToGidMapper(
        SntfFontTables fontTables,
        PdfFontFlags flags,
        bool substituted,
        PdfFontEncodingInfo encodingInfo,
        PdfCMap toUnicodeCMap)
    {
        _sntfTables = fontTables ?? throw new ArgumentNullException(nameof(fontTables));

        _flags = flags;
        _isSubstituted = substituted;
        _encoding = encodingInfo.BaseEncoding;
        _differences = encodingInfo.Differences;
        _toUnicodeCMap = toUnicodeCMap;
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// Uses identity mapping if specified; otherwise, resolves using encoding, name, or Unicode maps.
    /// Returns 0 if the mapping is not found.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code)
    {
        if (!_isSubstituted && _flags.HasFlag(PdfFontFlags.Symbolic) && _sntfTables.SingleByteCodeToGid != null)
        {
            return _sntfTables.SingleByteCodeToGid[code];
        }

        PdfString name = SingleByteEncodings.GetNameByCode(code, _encoding, _differences);

        if (name == null)
        {
            return 0;
        }

        if (_sntfTables.NameToGid.TryGetValue(name, out var gidByName))
        {
            return gidByName;
        }

        string unicode = _toUnicodeCMap?.GetUnicode(code);

        if (unicode == null)
        {
            AdobeGlyphList.CharacterMap.TryGetValue(name, out unicode);
        }

        if (unicode != null && _sntfTables.UnicodeToGid.TryGetValue(unicode, out var gidByUnicode))
        {
            return gidByUnicode;
        }

        return 0;
    }
}