using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.CffV2;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Resources;
using PdfPixel.Fonts.Sfnt;
using PdfPixel.Fonts.Type1;
using PdfPixel.Fonts.Typeface;
using PdfPixel.Models;
using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Handles simple (single-byte) fonts including Type 1, Type 1C, and TrueType variants.
/// Loads the embedded font data, builds the appropriate glyph-ID mapper, and resolves encoding.
/// </summary>
public class PdfSimpleFont : PdfSingleByteFont
{
    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly IPdfTypeface? _typeface;
    private readonly IByteCodeToGidMapper? _mapper;
    private readonly bool _isSubstituted;
    private readonly PdfSingleByteFontWidths? _standardFontWidths;

    internal PdfSimpleFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        (_typeface, _mapper, _isSubstituted) = GetTypefaceAndMapper();

        PdfStandardFontName? standardFontName = SubstitutionInfo.GetStandardName();
        if (standardFontName.HasValue)
        {
            _standardFontWidths = PdfSingleByteFontWidths.FromStandardFont(standardFontName.Value, SubstitutionInfo.IsBold, SubstitutionInfo.IsItalic, Encoding.BaseEncoding);
        }
    }

    /// <summary>
    /// Returns the advance width for the specified character code.
    /// Uses the font metrics table first, falls back to the glyph-ID mapper when the metrics entry is zero,
    /// and finally to the Standard 14 AFM widths when the font has no embedded metrics at all.
    /// </summary>
    /// <param name="code">The character code to retrieve the width for.</param>
    /// <returns>The advance width in user space units.</returns>
    public override float GetWidth(PdfCharacterCode code)
    {
        float width = base.GetWidth(code);

        // TODO: [MEDIUM] we need to use same fallaback for CID fonts
        if (width == 0 && _mapper != null)
        {
            width = (float)_mapper.GetWidth((byte)(code));
        }

        if (width == 0 && _standardFontWidths != null)
        {
            width = _standardFontWidths.GetWidth(code) ?? 0f;
        }

        return width;
    }

    /// <summary>
    /// The embedded or substituted typeface for this simple font.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => _typeface;

    /// <inheritdoc/>
    protected internal override bool IsSubstitutedFont => _isSubstituted;

    private (IPdfTypeface? Typeface, IByteCodeToGidMapper? Mapper, bool IsSubstituted) GetTypefaceAndMapper()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.Type1:
                {
                    ReadOnlyMemory<byte> type1RawData = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    CffTypeface? cffTypeface = Type1ToCffConverter.GetCffFont(
                        new Type1RawFontProgram(type1RawData, FontDescriptor.FontFileLength1, FontDescriptor.FontFileLength2),
                        Document.LoggerFactory);

                    if (cffTypeface == null)
                    {
                        _logger.LogWarning("Failed to get CFF font info for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to get CFF font info for font.");
                    }

                    if (Encoding.BaseEncoding == PdfEncoding.Unknown)
                    {
                        PdfFontEncoding? knownEncoding = SingleByteEncodings.GetEncodingByName(BaseFont.ToPdfFontString());
                        if (knownEncoding != null)
                        {
                            Encoding.UpdateEncoding(knownEncoding.Value.ToPdfEncoding());
                        }
                    }

                    // CodeToName for Font1 already contains base encoding vector, so, if Encoding.BaseEncoding is unknown,
                    // it will fallback to correct CodeToName
                    return BuildFromCffTypeface(cffTypeface);
                }
                case PdfFontFileFormat.Type1C:
                {
                    ReadOnlyMemory<byte> cffBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    return LoadFromCffBytes(cffBytes);
                }
                case PdfFontFileFormat.OpenType:
                {
                    ReadOnlyMemory<byte> openTypeBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    SfntFontProcessor sfntFontProcessor = new(Document.LoggerFactory);
                    SfntFont? sfntFont = sfntFontProcessor.Read(openTypeBytes);

                    if (sfntFont?.CffTypeface != null)
                    {
                        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
                        {
                            Encoding.UpdateEncoding(PdfEncoding.WinAnsiEncoding);
                        }

                        return BuildFromCffTypeface(sfntFont.CffTypeface);
                    }

                    return LoadFromSfntBytes(openTypeBytes);
                }
                case PdfFontFileFormat.TrueType:
                {
                    ReadOnlyMemory<byte> trueTypeBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;
                    return LoadFromSfntBytes(trueTypeBytes);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        PdfEncoding? standard14Encoding = SingleByteEncodings.GetEncodingByName(BaseFont.ToPdfFontString())?.ToPdfEncoding();

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(standard14Encoding ?? PdfEncoding.StandardEncoding);
        }

        // Standard 14 fonts resolve to a single well-known substitute, so a direct code-to-GID
        // mapper for that one typeface is reliable. Arbitrary non-embedded fonts may need a
        // different fallback typeface per glyph, which only the generic Unicode shaping path supports.
        if (standard14Encoding != null)
        {
            IPdfTypeface substituteTypeface = Document.FontProvider.GetTypeface(SubstitutionInfo, null, null);

            if (substituteTypeface is not TrueTypePdfTypeface trueTypeSubstituteTypeface)
            {
                _logger.LogWarning("Font provider returned a non-TrueType substitute ('{TypefaceType}') for standard 14 font '{FontName}'; falling back to no font.", substituteTypeface.GetType().Name, BaseFont);
                return (default, default, true);
            }

            SfntByteCodeToGidMapper substituteMapper = new(trueTypeSubstituteTypeface.SfntFont, FontDescriptor?.Flags ?? default, substituted: true, Encoding);

            return (trueTypeSubstituteTypeface, substituteMapper, true);
        }

        return (default, default, true);
    }

    /// <summary>
    /// Parses raw (unwrapped) CFF font data and loads it as the font's typeface. Shared by Type1C and
    /// CFF-flavored OpenType FontFile3 data.
    /// </summary>
    /// <param name="cffBytes">The raw CFF font program bytes.</param>
    private (IPdfTypeface? Typeface, IByteCodeToGidMapper? Mapper, bool IsSubstituted) LoadFromCffBytes(in ReadOnlyMemory<byte> cffBytes)
    {
        CffTypefaceReader cffTypefaceReader = new(Document.LoggerFactory);
        CffTypeface? cffTypeface = cffTypefaceReader.Read(cffBytes);

        if (cffTypeface == null)
        {
            _logger.LogWarning("Failed to parse embedded CFF font data for font '{FontName}'", BaseFont);
            throw new InvalidOperationException("Failed to parse embedded CFF font data.");
        }

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(PdfEncoding.WinAnsiEncoding);
        }

        return BuildFromCffTypeface(cffTypeface);
    }

    /// <summary>
    /// Builds the typeface and glyph-ID mapper from an already-parsed CFF typeface, merging its
    /// built-in encoding vector into <see cref="PdfSingleByteFont.Encoding"/> as a code-to-name fallback.
    /// </summary>
    /// <param name="cffTypeface">The parsed CFF typeface.</param>
    private (IPdfTypeface? Typeface, IByteCodeToGidMapper? Mapper, bool IsSubstituted) BuildFromCffTypeface(CffTypeface cffTypeface)
    {
        if (cffTypeface.Fonts.Length == 0)
        {
            _logger.LogWarning("Failed to get CFF font info for font '{FontName}'", BaseFont);
            throw new InvalidOperationException("Failed to get CFF font info for font.");
        }

        CffFont font = cffTypeface.Fonts[0];
        Encoding.MergeCodeToName(font.CodeToName);

        CffPdfTypeface typeface = new(cffTypeface);
        CffByteCodeToGidMapper mapper = new(cffTypeface, Encoding);

        return (typeface, mapper, false);
    }

    /// <summary>
    /// Loads a TrueType- or glyf-flavored OpenType font program directly and builds an sfnt-table-based
    /// code-to-GID mapper from it.
    /// </summary>
    /// <param name="sfntBytes">The raw sfnt font program bytes.</param>
    private (IPdfTypeface? Typeface, IByteCodeToGidMapper? Mapper, bool IsSubstituted) LoadFromSfntBytes(in ReadOnlyMemory<byte> sfntBytes)
    {
        TrueTypePdfTypeface typeface = new(sfntBytes, Document.LoggerFactory);

        if (FontDescriptor != null && (FontDescriptor.Flags & PdfFontFlags.Symbolic) == 0 && Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(PdfEncoding.WinAnsiEncoding);
        }

        SfntByteCodeToGidMapper mapper = new(typeface.SfntFont, FontDescriptor?.Flags ?? default, substituted: false, Encoding);

        return (typeface, mapper, false);
    }

    /// <summary>
    /// Returns the glyph ID (GID) for the specified character code by consulting the byte-code-to-GID mapper.
    /// Returns 0 when no mapper is available or the code is <see langword="null"/>.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID, or 0 if not found.</returns>
    public override ushort GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return 0;
        }

        if (_mapper == null)
        {
            return 0;
        }

        return _mapper.GetGid((byte)code);
    }
}
