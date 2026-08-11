using System.Runtime.InteropServices;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A single outline point in the whole font design units "glyf" stores, whose coordinates are int16
/// throughout the format: a simple glyph accumulates them from int16 deltas, and a composite's
/// components land back on the same grid.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct GlyphPoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphPoint"/> struct.
    /// </summary>
    /// <param name="x">The point's horizontal coordinate, in font design units.</param>
    /// <param name="y">The point's vertical coordinate, in font design units.</param>
    public GlyphPoint(short x, short y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Gets the point's horizontal coordinate, in font design units.
    /// </summary>
    public short X { get; }

    /// <summary>
    /// Gets the point's vertical coordinate, in font design units.
    /// </summary>
    public short Y { get; }

    /// <summary>
    /// Creates a point from coordinates that may have run outside the int16 range the format stores,
    /// clamping each of them back onto it.
    /// </summary>
    /// <param name="x">The horizontal coordinate to clamp.</param>
    /// <param name="y">The vertical coordinate to clamp.</param>
    public static GlyphPoint Clamped(int x, int y) => new(ClampToFontUnits(x), ClampToFontUnits(y));

    private static short ClampToFontUnits(int value)
    {
        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        return (short)value;
    }
}
