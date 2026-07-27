using PdfPixel.Color;

namespace PdfPixel.Commands;

/// <summary>
/// Command modifier that tints recorded content with a specific color. Used for uncolored tiling
/// patterns and uncolored Type 3 glyphs where the drawn shapes should adopt the current fill/stroke
/// color at replay time.
/// </summary>
public sealed class UncoloredPaintModifier
{
    /// <summary>
    /// Creates a modifier that tints all paint with the specified color.
    /// </summary>
    /// <param name="color">The tint color to apply.</param>
    public UncoloredPaintModifier(in PdfColor color) => Color = color;

    /// <summary>
    /// The tint color applied to modified paints.
    /// </summary>
    public PdfColor Color { get; }
}
