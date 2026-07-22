namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a parsed CFF Private DICT.
/// </summary>
public class CffPrivateDict
{
    /// <summary>
    /// Gets or sets the offset of the Local Subr INDEX, self-relative to the start of the Private DICT.
    /// </summary>
    public int? SubrsOffset { get; set; }

    /// <summary>
    /// Gets or sets the default glyph width, used when a charstring's width operand is absent.
    /// </summary>
    public float? DefaultWidthX { get; set; }

    /// <summary>
    /// Gets or sets the value a charstring's width operand (when present) is added to.
    /// </summary>
    public float? NominalWidthX { get; set; }
}
