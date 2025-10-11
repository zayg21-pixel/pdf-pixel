using HarfBuzzSharp;
using PdfReader.Fonts.Cff;
using SkiaSharp;
using System;
using System.IO;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Interface for font caching that contains both SKTypeface and HarfBuzz fonts
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    internal interface IFontCache : IDisposable
    {
        /// <summary>
        /// Retrieves the font data stream associated with the specified PDF font descriptor.
        /// </summary>
        /// <param name="decriptor">The <see cref="PdfFontDescriptor"/> that describes the font whose data stream is to be retrieved.</param>
        /// <returns>A <see cref="Stream"/> containing the font data. The caller is responsible for disposing of the stream when
        /// it is no longer needed.</returns>
        Stream GetFontStream(PdfFontDescriptor decriptor);

        /// <summary>
        /// Retrieves information about a Compact Font Format (CFF) font based on the specified PDF font descriptor.
        /// </summary>
        /// <param name="decriptor">The <see cref="PdfFontDescriptor"/> that provides metadata about the font.</param>
        /// <returns>A <see cref="CffNameKeyedInfo"/> object containing details about the CFF font, such as its name and keying
        /// information.</returns>
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
        /// Clear all cached fonts
        /// </summary>
        void ClearCache();
    }
}