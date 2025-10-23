using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
using SkiaSharp;
using System;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Interface for font caching SKTypeface and native font mappings.
    /// </summary>
    internal interface IFontCache : IDisposable
    {
        /// <summary>
        /// Get or create a SKTypeface for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get typeface for</param>
        /// <returns>SKTypeface instance</returns>
        SKTypeface GetTypeface(PdfFontBase font);

        /// <summary>
        /// Gets a glyph name to GID mapper for the specified font.
        /// Resolves a CFF mapper for CFF fonts, or an SFNT mapper for TrueType/OpenType fonts.
        /// Returns null for unsupported or non-TrueType font types.
        /// </summary>
        /// <param name="font">The PDF font to get the mapper for.</param>
        /// <returns>An IByteCodeToGidMapper for the font, or null if not available or not a TrueType/CFF font.</returns>
        IByteCodeToGidMapper GetByteCodeToGidMapper(PdfFontBase font);
    }
}