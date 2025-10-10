using HarfBuzzSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using System;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Creates HarfBuzz <see cref="Font"/> instances from parsed PDF font objects.
    /// This loader works only with embedded fonts (simple, CID, or composite with embedded descendant).
    /// Type3 fonts and non-embedded fonts are not shaped by HarfBuzz and return <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The loader pins the underlying decoded font bytes (already obtained and potentially cached by the font descriptor)
    /// to avoid an additional copy when constructing a HarfBuzz <see cref="Blob"/>. The memory is released when the blob is disposed.
    /// Font scale is initialized to a nominal 1000 units per em; higher level text layout code applies actual scaling.
    /// Any exception during creation is swallowed intentionally because complex shaping is an optional enhancement.
    /// </remarks>
    internal class HarfBuzzFontLoader
    {
        private readonly PdfDocument _document;
        private readonly ILogger<HarfBuzzFontLoader> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="HarfBuzzFontLoader"/> for the specified PDF document.
        /// </summary>
        /// <param name="document">Owning <see cref="PdfDocument"/> providing logging and font data access.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
        public HarfBuzzFontLoader(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = document.LoggerFactory.CreateLogger<HarfBuzzFontLoader>();
        }

        /// <summary>
        /// Creates a HarfBuzz <see cref="Font"/> for an embedded PDF font.
        /// Returns <c>null</c> when the font is not embedded, unsupported, or creation fails.
        /// </summary>
        /// <param name="pdfFont">Source PDF font abstraction.</param>
        /// <returns>Initialized HarfBuzz font or <c>null</c> if shaping is not possible.</returns>
        public Font CreateHarfBuzzFont(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
            {
                return null;
            }

            try
            {
                var face = CreateHarfBuzzFace(pdfFont);
                if (face == null)
                {
                    return null;
                }

                var font = new Font(face);
                font.SetScale(1000, 1000); // Nominal units per em; real sizing applied later.
                return font;
            }
            catch
            {
                // Intentionally ignored: advanced shaping is optional. Rendering will fall back to basic width metrics.
                return null;
            }
        }

        /// <summary>
        /// Creates an immutable HarfBuzz <see cref="Face"/> from the embedded font bytes represented by the given PDF font.
        /// </summary>
        /// <param name="pdfFont">Source PDF font.</param>
        /// <returns>Immutable HarfBuzz face or <c>null</c> when unsupported or invalid.</returns>
        private unsafe Face CreateHarfBuzzFace(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
            {
                return null;
            }

            ReadOnlyMemory<byte> fontData = default;
            switch (pdfFont)
            {
                case PdfSimpleFont simpleFont:
                    fontData = simpleFont.FontDescriptor?.GetFontStream() ?? default;
                    break;
                case PdfCIDFont cidFont:
                    fontData = cidFont.FontDescriptor?.GetFontStream() ?? default;
                    break;
                case PdfCompositeFont compositeFont:
                    var primaryDescendant = compositeFont.PrimaryDescendant;
                    if (primaryDescendant?.IsEmbedded == true)
                    {
                        fontData = primaryDescendant.FontDescriptor?.GetFontStream() ?? default;
                    }
                    else
                    {
                        _logger.LogWarning("Composite font {BaseFont} has no embedded descendant font", compositeFont.BaseFont);
                        return null;
                    }
                    break;
                case PdfType3Font:
                    _logger.LogWarning("Type3 font {BaseFont} cannot be used with HarfBuzz", pdfFont.BaseFont);
                    return null;
                default:
                    _logger.LogWarning("Unknown font type {FontType} for HarfBuzz font creation", pdfFont.GetType().Name);
                    return null;
            }

            if (fontData.IsEmpty)
            {
                _logger.LogWarning("Font {BaseFont} has no embedded font data", pdfFont.BaseFont);
                return null;
            }

            var memoryHandle = fontData.Pin();
            IntPtr pointer = (IntPtr)memoryHandle.Pointer;
            int length = fontData.Length;

            var blob = new Blob(pointer, length, MemoryMode.ReadOnly, () => memoryHandle.Dispose());

            var face = new Face(blob, 0);
            face.MakeImmutable();

            if (face.GlyphCount == 0)
            {
                face.Dispose();
                _logger.LogWarning("Font {BaseFont} has invalid font data (no glyphs)", pdfFont.BaseFont);
                return null;
            }

            return face;
        }
    }
}
