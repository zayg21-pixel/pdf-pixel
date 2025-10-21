namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Represents a shaped glyph with its width and optional additional advance after the glyph.
    /// </summary>
    public readonly struct ShapedGlyph
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShapedGlyph"/> struct.
        /// </summary>
        /// <param name="glyphId">The glyph identifier.</param>
        /// <param name="width">The width of the glyph.</param>
        /// <param name="advanceAfter">The additional advance after the glyph, such as for spaces.</param>
        public ShapedGlyph(uint glyphId, float width, float advanceAfter)
        {
            GlyphId = glyphId;
            Width = width;
            AdvanceAfter = advanceAfter;
        }

        /// <summary>
        /// Gets the glyph identifier.
        /// </summary>
        public uint GlyphId { get; }

        /// <summary>
        /// Gets the width of the glyph.
        /// </summary>
        public float Width { get; }

        /// <summary>
        /// Gets the additional advance after the glyph, such as for spaces.
        /// </summary>
        public float AdvanceAfter { get; }

        /// <summary>
        /// Gets the total width including advance after the glyph.
        /// </summary>
        public float TotalWidth => Width + AdvanceAfter;
    }
}