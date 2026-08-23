using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
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

    internal PdfSimpleFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        _resolution = ResolveTypeface();
    }

    /// <summary>
    /// The embedded or substituted typeface for this simple font.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => _resolution.Typeface;

    /// <inheritdoc/>
    protected internal override bool IsSubstitutedFont => _resolution.IsSubstituted;

    /// <summary>
    /// The descriptor this font's dictionary states, or - for a Standard 14 font, which the
    /// specification allows to state none - the one recovered from the resolved typeface's own metrics.
    /// </summary>
    public override PdfFontDescriptor? FontDescriptor => base.FontDescriptor ?? _resolution.Descriptor;

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
        if (_resolution.SubstitutesUndefinedCodesWithSpace
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

        if (width == null && _resolution.SpaceWidth != null && Encoding.GetNameByCode((byte)(uint)code).IsEmpty)
        {
            return _resolution.SpaceWidth;
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

        TypefaceResolution? standard14Resolution = ResolveStandard14Typeface();
        if (standard14Resolution != null)
        {
            return standard14Resolution.Value;
        }

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(PdfEncoding.StandardEncoding);
        }

        return new TypefaceResolution(
            default,
            default,
            isSubstituted: true,
            descriptor: null,
            Type == PdfFontSubType.Type1 || Type == PdfFontSubType.MMType1,
            ResolveSpaceWidth(mapper: null));
    }

    private TypefaceResolution? ResolveStandard14Typeface()
    {
        PdfStandardFontName? standardFontName = SubstitutionInfo.GetStandardName();
        if (!standardFontName.HasValue)
        {
            return null;
        }

        PdfFontEncoding? familyEncoding = SingleByteEncodings.GetDefaultEncoding(standardFontName.Value.ToPdfFontStandardName());
        if (familyEncoding == null)
        {
            return null;
        }

        SfntPdfTypeface typeface = Standard14TypefaceLoader.GetTypeface(
            standardFontName.Value.ToPdfFontStandardName(),
            SubstitutionInfo.IsBold,
            SubstitutionInfo.IsItalic,
            Document.LoggerFactory);

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            Encoding.UpdateEncoding(familyEncoding.Value.ToPdfEncoding());
        }

        PdfFontDescriptor descriptor = PdfFontDescriptor.FromMetrics(typeface.Metrics, typeface.IsSymbolEncoded);
        SfntByteCodeToGidMapper mapper = new(typeface, descriptor.Flags, substituted: false, Encoding);

        return new TypefaceResolution(
            typeface,
            mapper,
            isSubstituted: false,
            descriptor,
            Type == PdfFontSubType.Type1 || Type == PdfFontSubType.MMType1,
            ResolveSpaceWidth(mapper));
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

        return new TypefaceResolution(typeface, mapper, isSubstituted: false, descriptor: null, substitutesUndefinedCodesWithSpace: false, spaceWidth: null);
    }

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

                return new TypefaceResolution(typeface, mapper, isSubstituted: false, descriptor: null, substitutesUndefinedCodesWithSpace: false, spaceWidth: null);
            }
            default:
            {
                throw new InvalidOperationException($"Unexpected typeface type '{typeface.GetType()}' returned by {nameof(PdfTypefaceLoader)}.");
            }
        }
    }

    private float? ResolveSpaceWidth(SfntByteCodeToGidMapper? mapper)
    {
        byte? spaceCode = Encoding.GetCodeByName(SpaceGlyphName);

        if (spaceCode == null)
        {
            return null;
        }

        float? declaredWidth = base.GetWidth(spaceCode.Value);

        if (declaredWidth != null)
        {
            return declaredWidth;
        }

        return mapper?.GetWidth(spaceCode.Value);
    }

    private float? GetDefinedWidth(PdfCharacterCode code) => base.GetWidth(code) ?? _resolution.Mapper?.GetWidth((byte)(code));

    /// <summary>
    /// Result of resolving this font's typeface and glyph-ID mapper: whether it is substituted, and -
    /// only when the typeface is a Standard 14 one this font's dictionary describes no descriptor for -
    /// the descriptor recovered from that typeface.
    /// </summary>
    private readonly struct TypefaceResolution
    {
        public TypefaceResolution(
            IPdfTypeface? typeface,
            IByteCodeToGidMapper? mapper,
            bool isSubstituted,
            PdfFontDescriptor? descriptor,
            bool substitutesUndefinedCodesWithSpace,
            float? spaceWidth)
        {
            Typeface = typeface;
            Mapper = mapper;
            IsSubstituted = isSubstituted;
            Descriptor = descriptor;
            SubstitutesUndefinedCodesWithSpace = substitutesUndefinedCodesWithSpace;
            SpaceWidth = spaceWidth;
        }

        public IPdfTypeface? Typeface { get; }

        public IByteCodeToGidMapper? Mapper { get; }

        public bool IsSubstituted { get; }

        public PdfFontDescriptor? Descriptor { get; }

        public bool SubstitutesUndefinedCodesWithSpace { get; }

        public float? SpaceWidth { get; }
    }
}
