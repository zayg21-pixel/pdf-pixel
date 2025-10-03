using HarfBuzzSharp;
using SkiaSharp;
using System;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Interface for font caching that contains both SKTypeface and HarfBuzz fonts
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    internal interface IFontCache : IDisposable
    {
        ReadOnlyMemory<byte> GetFontStream(PdfFontDescriptor decriptor);

        CffNameKeyedInfo GetCffInfo(PdfFontDescriptor decriptor);


        /// <summary>
        /// Get or create a SKTypeface for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get typeface for</param>
        /// <returns>SKTypeface instance</returns>
        SKTypeface GetTypeface(PdfFontBase font);

        /// <summary>
        /// Retrieves a fallback typeface based on the specified font style parameters.
        /// </summary>
        /// <remarks>This method is useful for selecting a typeface that closely matches the desired font
        /// style when the primary typeface is unavailable.</remarks>
        /// <param name="weight">The weight of the font, typically ranging from 100 (thin) to 900 (black).</param>
        /// <param name="width">The width of the font, where lower values indicate narrower styles and higher values indicate wider styles.</param>
        /// <param name="slant">The slant of the font, indicating whether the style is upright, italic, or oblique.</param>
        /// <returns>A <see cref="SKTypeface"/> object representing the fallback typeface that matches the specified parameters.
        /// Returns <see langword="null"/> if no suitable fallback typeface is found.</returns>
        SKTypeface GetFallbackFromParameters(int weight, int width, SKFontStyleSlant slant);

        /// <summary>
        /// Get or create a HarfBuzz font for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get HarfBuzz font for</param>
        /// <returns>HarfBuzz Font instance, or null if not supported</returns>
        Font GetHarfBuzzFont(PdfFontBase font);

        /// <summary>
        /// Clear all cached fonts
        /// </summary>
        void ClearCache();
    }
}