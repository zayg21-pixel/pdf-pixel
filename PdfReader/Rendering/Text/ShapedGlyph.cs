namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Information about a shaped glyph
    /// </summary>
    public readonly struct ShapedGlyph
    {
        public ShapedGlyph(uint glyphId, float x, float y, float advanceX, float advanceY)
        {
            GlyphId = glyphId;
            X = x;
            Y = y;
            AdvanceX = advanceX;
            AdvanceY = advanceY;
        }

        public uint GlyphId { get; }
        public float X { get; }
        public float Y { get; }
        public float AdvanceX { get; }
        public float AdvanceY { get; }
    }
}