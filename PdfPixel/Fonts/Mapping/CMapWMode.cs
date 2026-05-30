namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Specifies the writing mode (WMode) of a CMap, as defined in the PDF specification.
/// </summary>
public enum CMapWMode
{
    /// <summary>
    /// Horizontal writing mode (WMode 0). The default for most Latin and CJK scripts written left-to-right or right-to-left.
    /// </summary>
    Horizontal = 0,

    /// <summary>
    /// Vertical writing mode (WMode 1). Used for top-to-bottom scripts such as traditional CJK typography.
    /// </summary>
    Vertical = 1
}
