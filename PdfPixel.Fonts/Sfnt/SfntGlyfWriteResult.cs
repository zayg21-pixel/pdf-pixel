namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The result of writing a "glyf" table: the table's own bytes, plus the "loca" table that must be
/// written alongside it, since "loca"'s offsets are only known once "glyf" has been laid out.
/// </summary>
public class SfntGlyfWriteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyfWriteResult"/> class.
    /// </summary>
    /// <param name="glyfData">The "glyf" table's raw content bytes.</param>
    /// <param name="loca">The "loca" table that indexes <paramref name="glyfData"/>.</param>
    public SfntGlyfWriteResult(byte[] glyfData, SfntLoca loca)
    {
        GlyfData = glyfData;
        Loca = loca;
    }

    /// <summary>
    /// Gets the "glyf" table's raw content bytes.
    /// </summary>
    public byte[] GlyfData { get; }

    /// <summary>
    /// Gets the "loca" table that indexes <see cref="GlyfData"/>.
    /// </summary>
    public SfntLoca Loca { get; }
}
