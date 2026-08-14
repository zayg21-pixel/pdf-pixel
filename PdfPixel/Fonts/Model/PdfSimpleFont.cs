using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Resources;
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
    private static readonly PdfFontString SpaceGlyphName = (PdfFontString)"space"u8;

    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly TypefaceResolution _resolution;
    private readonly bool _substitutesUndefinedCodesWithSpace;
    private readonly float? _spaceWidth;

    internal PdfSimpleFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        _resolution = ResolveTypeface();

        _substitutesUndefinedCodesWithSpace = _resolution.IsSubstituted
            && (Type == PdfFontSubType.Type1 || Type == PdfFontSubType.MMType1);

        if (_substitutesUndefinedCodesWithSpace)
        {
            _spaceWidth = ResolveSpaceWidth();
        }
    }

    /// <summary>
    /// The embedded or substituted typeface for this simple font.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => _resolution.Typeface;

    /// <inheritdoc/>
    protected internal override bool IsSubstitutedFont => _resolution.IsSubstituted;

    /// <summary>
    /// <see langword="true"/> when the font's own <c>/Widths</c> array has entries, or - for a
    /// substituted Standard 14 font with none - the AFM-derived fallback widths do.
    /// </summary>
    protected internal override bool HasWidths => base.HasWidths || _resolution.StandardFontWidths?.HasWidths == true;

    /// <summary>
    /// Returns the glyph ID (GID) for the specified character code by consulting the byte-code-to-GID mapper.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID, or <see langword="null"/> if not found.</returns>
    public override ushort? GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return null;
        }

        return _resolution.Mapper?.GetGid((byte)code);
    }

    /// <summary>
    /// Resolves glyphs for a single-byte code: the embedded program's own mapping first, then - because
    /// a simple font's code is a code, not a character - a symbol typeface's built-in encoding, and only
    /// then the Unicode the code stands for.
    /// </summary>
    protected override PdfGlyphResolution ResolveGlyphs(PdfCharacterCode characterCode, string? renderingUnicode)
    {
        if (!IsSubstitutedFont)
        {
            return ResolveFromFontProgram(characterCode, renderingUnicode);
        }

        int code = (byte)(uint)characterCode;
        SfntPdfTypeface? symbolTypeface = Document.FontProvider.GetSymbolTypefaceByCode(SubstitutionInfo, code);

        if (symbolTypeface != null)
        {
            return new PdfGlyphResolution(symbolTypeface, [symbolTypeface.GetGidByCode(code)], isMappedByFont: false);
        }

        return SubstituteByUnicode(renderingUnicode);
    }

    /// <inheritdoc/>
    public override string? GetRenderingUnicodeString(PdfCharacterCode code)
    {
        if (_substitutesUndefinedCodesWithSpace
            && Encoding.GetNameByCodeOrUndefined((byte)(uint)code) == SingleByteEncodings.UndefinedCharacter)
        {
            return " ";
        }

        return base.GetRenderingUnicodeString(code);
    }

    /// <summary>
    /// Returns the advance width for the specified character code.
    /// </summary>
    /// <param name="code">The character code to retrieve the width for.</param>
    /// <returns>The advance width in user space units.</returns>
    public override float? GetWidth(PdfCharacterCode code)
    {
        float? width = GetDefinedWidth(code);

        if (width == null && _spaceWidth != null && Encoding.GetNameByCode((byte)(uint)code).IsEmpty)
        {
            return _spaceWidth;
        }

        return width;
    }

    private TypefaceResolution ResolveTypeface()
    {
        try
        {
            PdfFontDescriptor? fontDescriptor = FontDescriptor;

            if (fontDescriptor?.FontFileStream != null)
            {
                PdfTypefaceLoader loader = new(fontDescriptor, Type, Document.LoggerFactory);
                IPdfTypeface typeface = loader.GetTypeface();

                if (fontDescriptor.FontFileFormat == PdfFontFileFormat.Type1 && typeface is CffPdfTypeface type1Typeface)
                {
                    if (Encoding.BaseEncoding == PdfEncoding.Unknown)
                    {
                        PdfFontEncoding? knownEncoding = (BaseFont == null) ? null : SingleByteEncodings.GetEncodingByName(BaseFont.Value.ToPdfFontString());
                        if (knownEncoding != null)
                        {
                            Encoding.UpdateEncoding(knownEncoding.Value.ToPdfEncoding());
                        }
                    }

                    // CodeToName for Font1 already contains base encoding vector, so, if Encoding.BaseEncoding is unknown,
                    // it will fallback to correct CodeToName
                    return BuildFromCffTypeface(type1Typeface);
                }

                return BuildFromTypeface(typeface);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        PdfEncoding? standard14Encoding = (BaseFont == null) ? null : SingleByteEncodings.GetEncodingByName(BaseFont.Value.ToPdfFontString())?.ToPdfEncoding();

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(standard14Encoding ?? PdfEncoding.StandardEncoding);
        }

        PdfSingleByteFontWidths? standardFontWidths = BuildStandardFontWidths();
        return new TypefaceResolution(default, default, isSubstituted: true, standardFontWidths);
    }

    /// <summary>
    /// Builds the typeface and glyph-ID mapper from an already-parsed CFF typeface, merging its
    /// built-in encoding vector into <see cref="PdfSingleByteFont.Encoding"/> as a code-to-name fallback.
    /// </summary>
    /// <param name="typeface">The CFF typeface.</param>
    private TypefaceResolution BuildFromCffTypeface(CffPdfTypeface typeface)
    {
        CffFont font = typeface.CffTypeface.Fonts[0];
        Encoding.MergeCodeToName(font.CodeToName);

        CffByteCodeToGidMapper mapper = new(typeface.CffTypeface, typeface, Encoding);

        return new TypefaceResolution(typeface, mapper, isSubstituted: false, standardFontWidths: null);
    }

    /// <summary>
    /// Builds the code-to-GID mapper matching the actual kind of typeface that was loaded: an sfnt
    /// cmap-based mapper for TrueType outlines, or a CFF built-in-encoding-based mapper for CFF
    /// outlines. Some fonts declare one embedded format but their font program turns out to hold the
    /// other, so this dispatches on the typeface actually returned rather than the declared format.
    /// </summary>
    private TypefaceResolution BuildFromTypeface(IPdfTypeface typeface)
    {
        switch (typeface)
        {
            case CffPdfTypeface cffPdfTypeface:
            {
                if (FontDescriptor != null && (FontDescriptor.Flags & PdfFontFlags.Symbolic) == 0 && Encoding.BaseEncoding == PdfEncoding.Unknown)
                {
                    Encoding.UpdateEncoding(PdfEncoding.WinAnsiEncoding);
                }

                return BuildFromCffTypeface(cffPdfTypeface);
            }
            case SfntPdfTypeface sfntPdfTypeface:
            {
                if (FontDescriptor != null && (FontDescriptor.Flags & PdfFontFlags.Symbolic) == 0 && Encoding.BaseEncoding == PdfEncoding.Unknown)
                {
                    Encoding.UpdateEncoding(PdfEncoding.WinAnsiEncoding);
                }

                SfntByteCodeToGidMapper mapper = new(sfntPdfTypeface, FontDescriptor?.Flags ?? default, substituted: false, Encoding);

                return new TypefaceResolution(typeface, mapper, isSubstituted: false, standardFontWidths: null);
            }
            default:
            {
                throw new InvalidOperationException($"Unexpected typeface type '{typeface.GetType()}' returned by {nameof(PdfTypefaceLoader)}.");
            }
        }
    }

    /// <summary>
    /// Returns the width the font gives the code its encoding maps to the space glyph, or
    /// <see langword="null"/> when the encoding maps no code to it.
    /// </summary>
    private float? ResolveSpaceWidth()
    {
        byte? spaceCode = Encoding.GetCodeByName(SpaceGlyphName);

        return (spaceCode.HasValue) ? GetDefinedWidth(spaceCode.Value) : null;
    }

    private float? GetDefinedWidth(PdfCharacterCode code) => base.GetWidth(code) ?? _resolution.Mapper?.GetWidth((byte)(code)) ?? _resolution.StandardFontWidths?.GetWidth(code);

    /// <summary>
    /// Builds the AFM-derived width table for the font this instance substitutes, or <see langword="null"/>
    /// when its name does not match a Standard 14 family.
    /// </summary>
    private PdfSingleByteFontWidths? BuildStandardFontWidths()
    {
        PdfStandardFontName? standardFontName = SubstitutionInfo.GetStandardName();
        if (!standardFontName.HasValue)
        {
            return null;
        }

        bool isBold = SubstitutionInfo.Weight >= PdfSubstitutionInfo.BoldWeight;
        return PdfSingleByteFontWidths.FromStandardFont(standardFontName.Value, isBold, SubstitutionInfo.IsItalic, Encoding);
    }

    /// <summary>
    /// Result of resolving this font's typeface and glyph-ID mapper: whether it is embedded or
    /// substituted, and - only when substituted - the AFM-derived widths for that substitution.
    /// </summary>
    private readonly struct TypefaceResolution
    {
        public TypefaceResolution(IPdfTypeface? typeface, IByteCodeToGidMapper? mapper, bool isSubstituted, PdfSingleByteFontWidths? standardFontWidths)
        {
            Typeface = typeface;
            Mapper = mapper;
            IsSubstituted = isSubstituted;
            StandardFontWidths = standardFontWidths;
        }

        public IPdfTypeface? Typeface { get; }

        public IByteCodeToGidMapper? Mapper { get; }

        public bool IsSubstituted { get; }

        public PdfSingleByteFontWidths? StandardFontWidths { get; }
    }
}
