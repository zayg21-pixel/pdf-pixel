using PdfPixel.Fonts.CffV2;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Provides mapping from PDF character codes to glyph IDs (GIDs) for CFF (Type 1C) fonts using parsed CFF metadata.
/// For single-byte fonts, resolves code to glyph name using encoding and differences, then maps name to GID via the CFF charset.
/// Name-to-GID is the only mapping available for CFF fonts; code-to-GID is achieved through encoding-to-name resolution.
/// </summary>
internal class CffByteCodeToGidMapper : IByteCodeToGidMapper
{
    private readonly ushort[] _codeToGid = new ushort[256];
    private readonly float[] _codeToWidth = new float[256];

    /// <summary>
    /// Initializes a new instance of <see cref="CffByteCodeToGidMapper"/> for the specified CFF typeface.
    /// </summary>
    /// <param name="cffTypeface">The parsed CFF typeface.</param>
    /// <param name="typeface">The typeface hosting <paramref name="cffTypeface"/>.</param>
    /// <param name="encodingInfo">The PDF font encoding.</param>
    public CffByteCodeToGidMapper(
        CffTypeface cffTypeface,
        IPdfTypeface typeface,
        PdfFontEncodingInfo encodingInfo)
    {
        if (cffTypeface == null)
        {
            throw new ArgumentNullException(nameof(cffTypeface));
        }

        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        CffFont font = cffTypeface.Fonts[0];
        IReadOnlyDictionary<PdfFontString, ushort>? nameToGid = font.NameToGid;

        if (nameToGid == null)
        {
            return;
        }

        for (int code = 0; code < 256; code++)
        {
            PdfFontString glyphName = encodingInfo.GetNameByCode((byte)code);

            if (!glyphName.IsEmpty && nameToGid.TryGetValue(glyphName, out ushort gid))
            {
                _codeToGid[code] = gid;
                _codeToWidth[code] = typeface.GetWidth(gid);
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
