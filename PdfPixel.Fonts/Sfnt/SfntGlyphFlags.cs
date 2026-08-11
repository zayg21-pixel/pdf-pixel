namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The per-point flag bits a simple "glyf" glyph stores ahead of its coordinates, shared by everything
/// that reads or writes that point stream.
/// </summary>
internal static class SfntGlyphFlags
{
    /// <summary>
    /// The point lies on the curve rather than being a control point.
    /// </summary>
    public const byte OnCurve = 0x01;

    /// <summary>
    /// The x delta is a single unsigned byte, whose sign <see cref="XSame"/> carries.
    /// </summary>
    public const byte XShort = 0x02;

    /// <summary>
    /// The y delta is a single unsigned byte, whose sign <see cref="YSame"/> carries.
    /// </summary>
    public const byte YShort = 0x04;

    /// <summary>
    /// This flag byte is followed by a count of additional points repeating it.
    /// </summary>
    public const byte Repeat = 0x08;

    /// <summary>
    /// With <see cref="XShort"/>, the x delta is positive; without it, x is unchanged.
    /// </summary>
    public const byte XSame = 0x10;

    /// <summary>
    /// With <see cref="YShort"/>, the y delta is positive; without it, y is unchanged.
    /// </summary>
    public const byte YSame = 0x20;
}
