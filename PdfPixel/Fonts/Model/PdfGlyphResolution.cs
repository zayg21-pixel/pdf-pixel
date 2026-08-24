namespace PdfPixel.Fonts.Model;

/// <summary>
/// The glyphs a PDF character code draws as, together with the typeface they belong to. Produced by
/// each font type's own resolution logic.
/// </summary>
public readonly struct PdfGlyphResolution
{
    /// <summary>
    /// Initializes a new <see cref="PdfGlyphResolution"/>.
    /// </summary>
    /// <param name="typeface">The typeface the glyph ids belong to.</param>
    /// <param name="glyphIds">The glyph ids the character code draws as. A <see langword="null"/> entry has no glyph and is not rendered.</param>
    /// <param name="isMappedByFont">Whether the glyph ids came from the PDF font's own code-to-glyph mapping.</param>
    public PdfGlyphResolution(IPdfTypeface typeface, ushort?[] glyphIds, bool isMappedByFont)
    {
        Typeface = typeface;
        GlyphIds = glyphIds;
        IsMappedByFont = isMappedByFont;
    }

    /// <summary>
    /// The typeface <see cref="GlyphIds"/> belong to, or <see langword="null"/> when nothing was resolved.
    /// </summary>
    public IPdfTypeface? Typeface { get; }

    /// <summary>
    /// The glyph ids the character code draws as, or <see langword="null"/> when nothing was resolved.
    /// A <see langword="null"/> entry means no glyph was found for that position and must not be rendered.
    /// </summary>
    public ushort?[]? GlyphIds { get; }

    /// <summary>
    /// <see langword="true"/> when the glyph ids came from the PDF font's own code-to-glyph mapping,
    /// so the font's declared advance width describes them directly. <see langword="false"/> when they
    /// were resolved from a substitute typeface, whose own advances have to be measured instead.
    /// </summary>
    public bool IsMappedByFont { get; }

    /// <summary>
    /// <see langword="true"/> when no typeface or no glyphs were resolved.
    /// </summary>
    public bool IsEmpty => Typeface == null || GlyphIds == null;
}
