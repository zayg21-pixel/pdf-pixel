using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for CFF (Type 1C) fonts using parsed CFF metadata.
/// For single-byte fonts, resolves code to glyph name using encoding and differences, then maps name to GID via CFF metadata.
/// Name-to-GID is the only mapping available for CFF fonts; code-to-GID is achieved through encoding-to-name resolution.
/// </summary>
internal class CffByteCodeToGidMapper : IByteCodeToGidMapper // TODO: this needs to be cleaned up significantly and optimized. We also need to extract font metrics from subroutines
{
    private readonly CffInfo _cffInfo;
    private readonly PdfFontFlags _flags;
    private readonly PdfFontEncoding _encoding;
    private readonly Dictionary<int, PdfString> _differences;

    /// <summary>
    /// Initializes a new instance of <see cref="CffByteCodeToGidMapper"/> for the specified CFF font info.
    /// </summary>
    /// <param name="cffInfo">The parsed CFF font metadata.</param>
    /// <param name="flags">Flags defined in PDF font.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    public CffByteCodeToGidMapper(
        CffInfo cffInfo,
        PdfFontFlags flags,
        PdfFontEncodingInfo encodingInfo)
    {
        _cffInfo = cffInfo ?? throw new ArgumentNullException(nameof(cffInfo));
        _flags = flags;
        _encoding = encodingInfo.BaseEncoding;
        _differences = encodingInfo.Differences;
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// Uses identity mapping if specified; otherwise, resolves using encoding or SID-to-GID map.
    /// Returns 0 if the mapping is not found.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code)
    {
        PdfString glyphName = SingleByteEncodings.GetNameByCode(code, _encoding, _differences);
        if (!glyphName.IsEmpty && _cffInfo.NameToGid.TryGetValue(glyphName, out ushort gidByName))
        {
            return gidByName;
        }

        return 0;
    }
}