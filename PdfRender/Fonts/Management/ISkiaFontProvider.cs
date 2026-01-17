using PdfRender.Fonts.Mapping;
using SkiaSharp;

namespace PdfRender.Fonts.Management;

/// <summary>
/// Provides SkiaSharp font resolution for standard PDF fonts and named fonts.
/// </summary>
public interface ISkiaFontProvider : System.IDisposable
{
    /// <summary>
    /// Gets a standard PDF font typeface by name and style, optionally ensuring it contains the specified unicode text.
    /// Returns null if no suitable typeface is found.
    /// </summary>
    /// <param name="standardFont">The standard PDF font family name.</param>
    /// <param name="style">Requested font style.</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <returns>An <see cref="SKTypeface"/> that matches the requested standard font and style; or <c>null</c> if not found.</returns>
    SKTypeface GetStandardFont(PdfStandardFontName standardFont, SKFontStyle style, string unicode);

    /// <summary>
    /// Gets a font typeface for a given family name and style, optionally ensuring it contains the specified unicode text.
    /// </summary>
    /// <param name="name">Font family name to resolve.</param>
    /// <param name="style">Requested font style.</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <returns>An <see cref="SKTypeface"/> that matches the requested font; or or <c>null</c> if not found.</returns>
    SKTypeface GetFont(string name, SKFontStyle style, string unicode);
}
