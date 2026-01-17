using PdfRender.Fonts.Mapping;
using SkiaSharp;
using System;

namespace PdfRender.Fonts.Management;

/// <summary>
/// Provides PDF font substitution and style detection using SkiaSharp.
/// </summary>
/// <remarks>
/// This class attempts to resolve non-embedded PDF fonts to available system fonts via an <see cref="ISkiaFontProvider"/>.
/// It matches fonts by normalized stem and style, falling back to known family substitutions if necessary.
/// Resolved typefaces are cached by <see cref="PdfFontName"/>.
/// </remarks>
internal sealed class SkiaFontSubstitutor
{
    private ISkiaFontProvider _skiaFontProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkiaFontSubstitutor"/> class.
    /// </summary>
    /// <param name="skiaFontProvider">The font provider used for font resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="skiaFontProvider"/> is null.</exception>
    public SkiaFontSubstitutor(ISkiaFontProvider skiaFontProvider)
    {
        _skiaFontProvider = skiaFontProvider ?? throw new ArgumentNullException(nameof(skiaFontProvider));
    }

    /// <summary>
    /// Resolves a substitute <see cref="SKTypeface"/> for a non-embedded PDF font.
    /// Attempts to match by normalized stem and style, then falls back to known family substitutions.
    /// </summary>
    /// <param name="substitutionInfo">Font substitution information, including normalized stem and style.</param>
    /// <param name="unicode">Optional unicode text to validate glyph availability.</param>
    /// <returns>
    /// A matching <see cref="SKTypeface"/> if found; otherwise, <see cref="SKTypeface.Default"/>.
    /// </returns>
    public SKTypeface SubstituteTypeface(PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        SKFontStyle style = substitutionInfo.FontStyle;

        if (Enum.TryParse<PdfStandardFontName>(substitutionInfo.NormalizedStem, out var standardFont))
        {
            var standardTypeface = _skiaFontProvider.GetStandardFont(standardFont, substitutionInfo.FontStyle, unicode);

            if (standardTypeface != null)
            {
                return standardTypeface;
            }
        }

        return _skiaFontProvider.GetFont(substitutionInfo.NormalizedStem, substitutionInfo.FontStyle, unicode) ?? SKTypeface.Default;
    }
}