namespace PdfRender.Fonts.Mapping;

/// <summary>
/// Represents the set of standard PDF font families, including a fallback <see cref="Default"/> value.
/// </summary>
/// <remarks>
/// The values correspond to common Standard 14 font family names used in PDF documents.
/// </remarks>
public enum PdfStandardFontName
{
    /// <summary>
    /// Times family.
    /// </summary>
    Times,

    /// <summary>
    /// Times New Roman family.
    /// </summary>
    TimesNewRoman,

    /// <summary>
    /// Times New Roman PS family.
    /// </summary>
    TimesNewRomanPS,

    /// <summary>
    /// Helvetica family.
    /// </summary>
    Helvetica,

    /// <summary>
    /// Arial family.
    /// </summary>
    Arial,

    /// <summary>
    /// Courier family.
    /// </summary>
    Courier,

    /// <summary>
    /// Courier New family.
    /// </summary>
    CourierNew,

    /// <summary>
    /// Courier New PS family.
    /// </summary>
    CourierNewPS,

    /// <summary>
    /// Symbol family.
    /// </summary>
    Symbol,

    /// <summary>
    /// Zapf Dingbats family.
    /// </summary>
    ZapfDingbats,
}
