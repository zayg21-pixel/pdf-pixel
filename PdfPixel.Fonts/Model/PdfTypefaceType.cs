namespace PdfPixel.Fonts.Model;

/// <summary>
/// Which outline format a font program uses, telling <c>PdfPixel.Fonts.Typeface.PdfTypefaceFactory</c>
/// which <see cref="IPdfTypeface"/> implementation to build.
/// </summary>
public enum PdfTypefaceType
{
    /// <summary>
    /// PostScript (Type 2 charstring) outlines: a bare CFF font program.
    /// </summary>
    Cff,

    /// <summary>
    /// TrueType (quadratic spline) outlines: an SFNT font program with a "glyf" table.
    /// </summary>
    TrueType
}
