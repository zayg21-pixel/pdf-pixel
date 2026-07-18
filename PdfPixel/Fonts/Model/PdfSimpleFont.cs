using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.TrueType;
using PdfPixel.Fonts.Type1;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Handles simple (single-byte) fonts including Type 1, Type 1C, and TrueType variants.
/// Loads the embedded font data, builds the appropriate glyph-ID mapper, and resolves encoding.
/// </summary>
public class PdfSimpleFont : PdfSingleByteFont
{
    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly SKTypeface? _typeface;
    private readonly IByteCodeToGidMapper? _mapper;
    private readonly bool _isSubstituted;
    private readonly SingleByteFontWidths? _standardFontWidths;

    internal PdfSimpleFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        (_typeface, _mapper, _isSubstituted) = GetTypefaceAndMapper();

        PdfStandardFontName? standardFontName = SubstitutionInfo.GetStandardName();
        if (standardFontName.HasValue)
        {
            _standardFontWidths = SingleByteFontWidths.FromStandardFont(standardFontName.Value, SubstitutionInfo.IsBold, SubstitutionInfo.IsItalic, Encoding.BaseEncoding);
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
    /// The embedded or substituted SkiaSharp typeface for this simple font.
    /// </summary>
    protected internal override SKTypeface? Typeface => _typeface;

    /// <inheritdoc/>
    protected internal override bool IsSubstitutedFont => _isSubstituted;

    private (SKTypeface? Typeface, IByteCodeToGidMapper? Mapper, bool IsSubstituted) GetTypefaceAndMapper()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.Type1:
                {
                    CffInfo? cffInfo = Type1ToCffConverter.GetCffFont(FontDescriptor, Document.LoggerFactory);
                    byte[]? typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    SKTypeface typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded Type1 font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded Type1 font data.");
                    }

                    if (cffInfo == null)
                    {
                        _logger.LogWarning("Failed to get CFF font info for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to get CFF font info for font.");
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        PdfFontEncoding? knownEncoding = SingleByteEncodings.GetEncodingByName(BaseFont);
                        if (knownEncoding != null)
                        {
                            Encoding.UpdateEncoding(knownEncoding.Value);
                        }
                    }

                    // CodeToName for Font1 already contains base encoding vector, so, if Encoding.BaseEncoding is unknown,
                    // it will fallback to correct CodeToName
                    Encoding.MergeCodeToName(cffInfo.CodeToName);

                    CffByteCodeToGidMapper mapper = new(cffInfo, Encoding);

                    return (typeface, mapper, false);
                }
                case PdfFontFileFormat.Type1C:
                {
                    CffSidGidMapper cffSidMapper = new(Document.LoggerFactory);
                    ReadOnlyMemory<byte> cffBytes = FontDescriptor.FontFileStream?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;

                    if (!cffSidMapper.TryParseNameKeyed(cffBytes, out CffInfo? cffInfo) || cffInfo == null)
                    {
                        _logger.LogWarning("Failed to parse embedded Type1C font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to parse embedded Type1C font data.");
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.UpdateEncoding(cffInfo.Encoding);
                    }

                    Encoding.MergeCodeToName(cffInfo.CodeToName);

                    byte[]? typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    using SKData skTypefaceData = SKData.CreateCopy(typefaceData);
                    SKTypeface typeface = SKTypeface.FromData(skTypefaceData);

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded Type1C font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded Type1C font data.");
                    }

                    CffByteCodeToGidMapper mapper = new(cffInfo, Encoding);

                    return (typeface, mapper, false);
                }
                case PdfFontFileFormat.TrueType:
                {
                    SKTypeface typeface = SKTypeface.FromStream(FontDescriptor.FontFileStream?.DecodeAsStream());

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded TrueType font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded TrueType font data.");
                    }

                    SfntFontTables sfntTables = SfntFontTablesParser.GetSfntFontTables(typeface);

                    if ((FontDescriptor.Flags & PdfFontFlags.Symbolic) == 0 && Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.UpdateEncoding(PdfFontEncoding.WinAnsiEncoding);
                    }

                    SfntByteCodeToGidMapper mapper = new(sfntTables, FontDescriptor.Flags, substituted: false, Encoding);

                    return (typeface, mapper, false);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        PdfFontEncoding? standard14Encoding = SingleByteEncodings.GetEncodingByName(BaseFont);

        if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
        {
            Encoding.UpdateEncoding(standard14Encoding ?? PdfFontEncoding.StandardEncoding);
        }

        // Standard 14 fonts resolve to a single well-known substitute, so a direct code-to-GID
        // mapper for that one typeface is reliable. Arbitrary non-embedded fonts may need a
        // different fallback typeface per glyph, which only the generic Unicode shaping path supports.
        if (standard14Encoding != null)
        {
            SKTypeface substituteTypeface = Document.FontSubstitutor.SubstituteTypeface(SubstitutionInfo, null, null);
            SfntFontTables substituteSfntTables = SfntFontTablesParser.GetSfntFontTables(substituteTypeface);
            SfntByteCodeToGidMapper substituteMapper = new(substituteSfntTables, FontDescriptor?.Flags ?? default, substituted: true, Encoding);

            return (substituteTypeface, substituteMapper, true);
        }

        return (default, default, true);
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _typeface?.Dispose();
        base.Dispose(disposing);
    }
}
