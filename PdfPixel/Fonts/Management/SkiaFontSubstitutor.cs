using PdfPixel.Fonts.Mapping;
using SkiaSharp;
using System;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Provides PDF font substitution and style detection using SkiaSharp.
/// </summary>
/// <remarks>
/// This class attempts to resolve non-embedded PDF fonts to available system fonts via an <see cref="ISkiaFontProvider"/>.
/// It matches fonts by normalized stem and style, falling back to known family substitutions if necessary.
/// Resolved typefaces are cached by <see cref="PdfSubstitutionInfo"/>.
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
    /// Resolves a substitute <see cref="SKTypeface"/> for a non-embedded PDF font.
    /// Attempts to match by normalized stem and style, then falls back to known family substitutions.
    /// When <paramref name="width"/> is provided, the provider may return a variation-adjusted typeface.
    /// </summary>
    /// <param name="substitutionInfo">Font substitution information, including normalized stem and style.</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <param name="width">Optional horizontal scale hint (1.0 = normal). Mapped to the <c>wdth</c> axis when available.</param>
    /// <returns>
    /// A matching <see cref="SKTypeface"/> if found; otherwise, <see cref="SKTypeface.Default"/>.
    /// </returns>
    public SKTypeface SubstituteTypeface(in PdfSubstitutionInfo substitutionInfo, string? unicode, float? width)
    {
        if (Enum.TryParse(substitutionInfo.NormalizedStem, out PdfStandardFontName standardFont))
        {
            SKTypeface? standardTypeface = _skiaFontProvider.GetStandardFont(standardFont, substitutionInfo.FontStyle, unicode, width);

            if (standardTypeface != null)
            {
                return standardTypeface;
            }
        }

        return _skiaFontProvider.GetFont(substitutionInfo.NormalizedStem, substitutionInfo.FontStyle, unicode, width) ?? SKTypeface.Default;
    }
}
