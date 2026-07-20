using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts;
using System;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Provides PDF font substitution and style detection using SkiaSharp.
/// </summary>
/// <remarks>
/// This class attempts to resolve non-embedded PDF fonts to available system fonts via an <see cref="ISkiaFontProvider"/>.
/// It matches fonts by normalized stem and style, falling back to known family substitutions if necessary.
/// </remarks>
internal sealed class SkiaFontSubstitutor
{
    private readonly ISkiaFontProvider _skiaFontProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaFontSubstitutor"/> class.
    /// </summary>
    /// <param name="skiaFontProvider">The font provider used for font resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="skiaFontProvider"/> is null.</exception>
    public SkiaFontSubstitutor(ISkiaFontProvider skiaFontProvider) => _skiaFontProvider = skiaFontProvider ?? throw new ArgumentNullException(nameof(skiaFontProvider));

    /// <summary>
    /// Resolves a substitute <see cref="PdfTypeface"/> for a non-embedded PDF font.
    /// Attempts to match by normalized stem and style, then falls back to known family substitutions.
    /// </summary>
    /// <param name="substitutionInfo">Font substitution information, including normalized stem and style.</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <param name="width">Optional horizontal scale hint (1.0 = normal).</param>
    /// <returns>A matching <see cref="PdfTypeface"/>.</returns>
    public PdfTypeface SubstituteTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width)
    {
        PdfStandardFontName? standardFont = substitutionInfo.GetStandardName();
        if (standardFont.HasValue)
        {
            PdfTypeface? standardTypeface = _skiaFontProvider.GetStandardFont(standardFont.Value, substitutionInfo.FontStyle, unicode, width);

            if (standardTypeface != null)
            {
                return standardTypeface;
            }
        }

        return _skiaFontProvider.GetFont(substitutionInfo.NormalizedStem, substitutionInfo.FontStyle, unicode, width);
    }
}
