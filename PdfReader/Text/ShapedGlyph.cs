namespace PdfReader.Text;

/// <summary>
/// Represents a shaped glyph with its width and optional additional advance after the glyph.
/// </summary>
public readonly struct ShapedGlyph
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShapedGlyph"/> struct.
    /// </summary>
    /// <param name="glyphId">The glyph identifier.</param>
    /// <param name="unicode">Unique representation of the glyph.</param>
    /// <param name="width">Shaped width of the glyph.</param>
    /// <param name="x">The X position of the glyph.</param>
    /// <param name="y">The Y position of the glyph.</param>
    public ShapedGlyph(uint glyphId, string unicode, float width, float x, float y)
    {
        GlyphId = glyphId;
        Unicode = unicode;
        Width = width;
        X = x;
        Y = y;
    }

    /// <summary>
    /// Gets the glyph identifier.
    /// </summary>
    public uint GlyphId { get; }

    /// <summary>
    /// Unique representation of the glyph.
    /// </summary>
    public string Unicode { get; }

    /// <summary>
    /// Shaped width of the glyph.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// X position of the glyph.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Y position of the glyph.
    /// </summary>
    public float Y { get; } 
}