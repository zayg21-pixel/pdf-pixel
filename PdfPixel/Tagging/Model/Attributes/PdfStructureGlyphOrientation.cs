namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Glyph orientation in vertical text (GlyphOrientationVertical), given either as an angle or as a named mode.
/// </summary>
public readonly struct PdfStructureGlyphOrientation
{
    internal PdfStructureGlyphOrientation(int angle)
        => Angle = angle;

    internal PdfStructureGlyphOrientation(PdfStructureGlyphOrientationMode mode)
        => Mode = mode;

    /// <summary>
    /// Clockwise rotation in degrees, a multiple of 90 between -180 and 360, or <see langword="null"/> when given as <see cref="Mode"/>.
    /// </summary>
    public int? Angle { get; }

    /// <summary>
    /// Named mode, or <see langword="null"/> when given as <see cref="Angle"/>.
    /// </summary>
    public PdfStructureGlyphOrientationMode? Mode { get; }
}
