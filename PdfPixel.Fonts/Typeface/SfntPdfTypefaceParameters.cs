using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Typeface;

/// <summary>
/// Optional construction parameters for <see cref="SfntPdfTypeface"/>.
/// </summary>
public sealed class SfntPdfTypefaceParameters
{
    /// <summary>
    /// When <see langword="true"/> (the default), repacks the parsed font into a fresh SFNT byte
    /// stream served by <see cref="IPdfTypeface.GetFontStream"/>. When <see langword="false"/>, the
    /// original input bytes are served as-is instead, skipping the repack cost.
    /// </summary>
    public bool RepackTypeface { get; set; } = true;

    /// <summary>
    /// The zero-based index, within a TrueType Collection ("ttcf") file, of the font to read. Ignored
    /// for a plain (non-collection) sfnt file.
    /// </summary>
    public int TtcIndex { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the font program comes from a font installed on the system, and
    /// <see cref="PdfFontMetrics"/> names the font the substitutor resolved it from.
    /// </summary>
    public bool IsSystemFont { get; set; }

    /// <summary>
    /// The glyph subset and character mapping the repack writes. Null repacks the font as it stands.
    /// Ignored when <see cref="RepackTypeface"/> is <see langword="false"/>.
    /// </summary>
    public SfntPdfTypefaceRepackParameters? Repack { get; set; }

    /// <summary>
    /// Gets the default parameters: <see cref="RepackTypeface"/> = <see langword="true"/>,
    /// <see cref="TtcIndex"/> = 0, <see cref="IsSystemFont"/> = <see langword="false"/>.
    /// </summary>
    public static SfntPdfTypefaceParameters Default { get; } = new();
}
