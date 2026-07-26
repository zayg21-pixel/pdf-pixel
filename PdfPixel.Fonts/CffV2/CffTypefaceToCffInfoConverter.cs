using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Converts a parsed <see cref="CffTypeface"/> into the legacy <see cref="CffInfo"/> shape.
/// </summary>
public static class CffTypefaceToCffInfoConverter
{
    /// <summary>
    /// Converts a typeface's first font to a <see cref="CffInfo"/>.
    /// </summary>
    /// <param name="typeface">The parsed typeface.</param>
    /// <param name="cffData">The raw CFF bytes the typeface was parsed from.</param>
    public static CffInfo? Convert(CffTypeface typeface, in ReadOnlyMemory<byte> cffData)
    {
        if (typeface == null || typeface.Fonts.Length == 0)
        {
            return null;
        }

        CffFont font = typeface.Fonts[0];
        bool isCidFont = font.FdArray.Length > 0;

        var gidWidths = new float[font.Characters.Length];
        for (int glyphIndex = 0; glyphIndex < font.Characters.Length; glyphIndex++)
        {
            CffCharacter character = font.Characters[glyphIndex];
            gidWidths[glyphIndex] = character.Width * character.GetEffectiveMatrix(font).ScaleX;
        }

        Dictionary<PdfFontString, ushort>? nameToGid = null;
        PdfFontString[]? codeToName = null;
        var encoding = PdfFontEncoding.Unknown;

        if (!isCidFont && font.Charset != null)
        {
            nameToGid = BuildNameToGid(font.Charset, typeface.Strings);

            if (font.Encoding != null)
            {
                codeToName = BuildCodeToName(font.Encoding, font.Charset, typeface.Strings);
                encoding = font.Encoding.PredefinedId switch
                {
                    CffConstants.EncodingPredefinedStandard => PdfFontEncoding.StandardEncoding,
                    CffConstants.EncodingPredefinedExpert => PdfFontEncoding.MacExpertEncoding,
                    _ => PdfFontEncoding.Unknown
                };
            }
        }

        return new CffInfo
        {
            GlyphCount = font.Characters.Length,
            NameToGid = nameToGid,
            CodeToName = codeToName,
            IsCidFont = isCidFont,
            Encoding = encoding,
            GidToSid = font.Charset?.SidsByGid,
            GidWidths = gidWidths,
            CffData = cffData,
            FontMatrix = font.Dict.TopDict.FontMatrix?.ToArray()
        };
    }

    private static Dictionary<PdfFontString, ushort> BuildNameToGid(CffCharset charset, ReadOnlyMemory<byte>[] strings)
    {
        Dictionary<PdfFontString, ushort> nameToGid = [];
        ushort[] sidsByGid = charset.SidsByGid;
        for (ushort gid = 0; gid < sidsByGid.Length; gid++)
        {
            PdfFontString name = CffCharset.ResolveGlyphName(sidsByGid[gid], strings);
            if (!name.IsEmpty && !nameToGid.ContainsKey(name))
            {
                nameToGid[name] = gid;
            }
        }

        return nameToGid;
    }

    private static PdfFontString[] BuildCodeToName(CffEncoding encoding, CffCharset charset, ReadOnlyMemory<byte>[] strings)
    {
        var codeToName = new PdfFontString[256];
        for (int code = 0; code < codeToName.Length; code++)
        {
            ushort gid = encoding.GidByCode[code];
            if (gid >= charset.SidsByGid.Length)
            {
                continue;
            }

            codeToName[code] = CffCharset.ResolveGlyphName(charset.SidsByGid[gid], strings);
        }

        return codeToName;
    }
}
