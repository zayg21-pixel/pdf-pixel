using PdfPixel.Objects;
using PdfPixel.Rendering;
using PdfPixel.Text;
using System;

namespace PdfPixel.Fonts;

/// <summary>
/// A font known only by its name, drawing Unicode text. A character code is the Unicode code point it stands for.
/// </summary>
internal sealed class PdfNamedFont : PdfFont
{
    private readonly SfntPdfTypeface? _typeface;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNamedFont"/> class.
    /// </summary>
    /// <param name="document">PDF document the font is used in</param>
    /// <param name="baseFont">Base font name (PostScript name)</param>
    public PdfNamedFont(IPdfDocumentInternal document, in PdfString baseFont)
        : base(document, baseFont)
    {
        PdfStandardFontName? standardFontName = SubstitutionInfo.GetStandardName();
        if (standardFontName != null)
        {
            _typeface = Standard14TypefaceLoader.GetTypeface(
                standardFontName.Value.ToPdfFontStandardName(),
                SubstitutionInfo.IsBold,
                SubstitutionInfo.IsItalic,
                document.LoggerFactory);
        }
    }

    /// <inheritdoc />
    protected internal override IPdfTypeface? Typeface => _typeface;

    /// <inheritdoc />
    protected internal override bool HasWidths => false;

    /// <summary>
    /// Shapes <paramref name="text"/> into <paramref name="buffer"/>, which is cleared first. Characters the
    /// font's typeface has no glyph for are drawn from a substitute.
    /// </summary>
    /// <param name="text">The Unicode text to shape.</param>
    /// <param name="state">The graphics state supplying font size and spacing.</param>
    /// <param name="buffer">The buffer to clear and fill with the shaped glyphs.</param>
    public void Shape(string text, PdfGraphicsState state, ShapedGlyphBuffer buffer)
    {
        var codes = new PdfCharacterCode[text.Length];
        int count = 0;
        int index = 0;
        while (index < text.Length)
        {
            int codePoint = text.GetCodePoint(index, out int length);
            index += length;
            codes[count] = GetCharacterCode(codePoint);
            count++;
        }

        ShapedGlyphBuilder.BuildFromCodes(codes.AsSpan(0, count), this, state, buffer);
    }

    /// <inheritdoc />
    public override float? GetWidth(in PdfCharacterCode code) => null;

    /// <inheritdoc />
    public override PdfVerticalMetric GetVerticalDisplacement(in PdfCharacterCode code) => default;

    /// <inheritdoc />
    public override string? GetUnicodeString(in PdfCharacterCode code) => char.ConvertFromUtf32((int)code.Value);

    /// <inheritdoc />
    public override PdfCharacterCode[] ExtractCharacterCodes(ReadOnlyMemory<byte> bytes)
    {
        var codes = new PdfCharacterCode[bytes.Length];
        for (int index = 0; index < codes.Length; index++)
        {
            codes[index] = GetCharacterCode(bytes.Span[index]);
        }

        return codes;
    }

    /// <inheritdoc />
    public override ushort? GetGid(in PdfCharacterCode code) => _typeface?.GetGid(char.ConvertFromUtf32((int)code.Value));

    /// <inheritdoc />
    protected override PdfGlyphResolution ResolveGlyphs(in PdfCharacterCode characterCode, string? renderingUnicode)
    {
        ushort? gid = GetGid(characterCode);
        if (_typeface != null && gid != null)
        {
            return new PdfGlyphResolution(_typeface, [gid], isMappedByFont: true);
        }

        return SubstituteByUnicode(renderingUnicode);
    }

    private static PdfCharacterCode GetCharacterCode(int codePoint)
        => new((uint)codePoint, (codePoint <= byte.MaxValue) ? 1 : sizeof(uint));
}
