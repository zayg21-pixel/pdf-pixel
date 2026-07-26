using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Resolves an <see cref="IPdfTypeface"/> for standard PDF fonts and named fonts.
/// </summary>
public interface IFontProvider
{
    /// <summary>
    /// Gets a typeface matching the given substitution info, optionally ensuring it contains the specified
    /// unicode text. Falls back to a last-resort typeface when no better match is found.
    /// </summary>
    /// <param name="substitutionInfo">Normalized font substitution hints (stem, bold, italic).</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <param name="width">Optional horizontal scale hint (1.0 = normal).</param>
    /// <returns>A matching <see cref="IPdfTypeface"/>.</returns>
    IPdfTypeface GetTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width);
}
