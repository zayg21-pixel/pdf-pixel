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
internal class CffByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly ushort[] _codeToGid = new ushort[256];
    private readonly float[] _codeToWidth = new float[256];

    /// <summary>
    /// Initializes a new instance of <see cref="CffByteCodeToGidMapper"/> for the specified CFF font info.
    /// </summary>
    /// <param name="cffInfo">The parsed CFF font metadata.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    public CffByteCodeToGidMapper(
        CffInfo cffInfo,
        PdfFontEncodingInfo encodingInfo)
    {
        if (cffInfo == null)
        {
            throw new ArgumentNullException(nameof(cffInfo));
        }

        PdfFontEncoding encoding = encodingInfo.BaseEncoding;
        Dictionary<int, PdfString> differences = encodingInfo.Differences;

        for (int code = 0; code < 256; code++)
        {
            PdfString glyphName = SingleByteEncodings.GetNameByCode((byte)code, encoding, differences);

            if (cffInfo.NameToGid != null && !glyphName.IsEmpty && cffInfo.NameToGid.TryGetValue(glyphName, out ushort gid))
            {
                _codeToGid[code] = gid;

                if (cffInfo.GidWidths != null && gid < cffInfo.GidWidths.Length)
                {
                    _codeToWidth[code] = cffInfo.GidWidths[gid];
                }
            }
        }
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code.
    /// Returns 0 if the mapping is not found.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public ushort GetGid(byte code) => _codeToGid[code];

    /// <summary>
    /// Gets the glyph width for the specified character code.
    /// Returns the width from CFF charstring metrics, or 0 if not found.
    /// </summary>
    /// <param name="code">The PDF character code.</param>
    /// <returns>The glyph width for the character code, or 0 if not found.</returns>
    public float GetWidth(byte code) => _codeToWidth[code];
}
