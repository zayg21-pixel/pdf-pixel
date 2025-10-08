using HarfBuzzSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Models;
using System;

namespace PdfReader.Fonts
{
    internal class HarfBuzzFontLoader
    {
        private readonly PdfDocument _document;
        private readonly ILogger<HarfBuzzFontLoader> _logger;

        public HarfBuzzFontLoader(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = document.LoggerFactory.CreateLogger<HarfBuzzFontLoader>();
        }

        public Font CreateHarfBuzzFont(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
            {
                return null;
            }

            try
            {
                // Get or create face based on font type
                var face = CreateHarfBuzzFace(pdfFont);
                if (face == null)
                {
                    return null;
                }

                // Create font from face
                var font = new Font(face);

                // Set font size (will be scaled later based on actual text size)
                font.SetScale(1000, 1000); // Use 1000 units per em as base

                return font;
            }
            catch
            {
                // Swallowing exceptions intentionally here; font shaping is optional.
                return null;
            }
        }

        private unsafe Face CreateHarfBuzzFace(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
            {
                return null;
            }

            // Get font data based on font type and descriptor
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
                    // For composite fonts, get data from primary descendant
                    var primaryDescendant = compositeFont.PrimaryDescendant;
                    if (primaryDescendant?.IsEmbedded == true)
                    {
                        fontData = primaryDescendant.FontDescriptor?.GetFontStream() ?? default;
                    }
                    else
                    {
                        _logger.LogWarning("Warning: Composite font {BaseFont} has no embedded descendant font", compositeFont.BaseFont);
                        return null;
                    }
                    break;
                case PdfType3Font:
                    // Type3 fonts don't have embedded font files - they use procedural glyphs
                    _logger.LogWarning("Warning: Type3 font {BaseFont} cannot be used with HarfBuzz", pdfFont.BaseFont);
                    return null;
                default:
                    _logger.LogWarning("Warning: Unknown font type {FontType} for HarfBuzz font creation", pdfFont.GetType().Name);
                    return null;
            }

            if (fontData.IsEmpty)
            {
                _logger.LogWarning("Warning: Font {BaseFont} has no embedded font data", pdfFont.BaseFont);
                return null;
            }

            // GetFontStream already returns decoded and wrapped data when necessary
            // Pin the existing ReadOnlyMemory to avoid copying into a new array
            var memoryHandle = fontData.Pin();
            IntPtr pointer = (IntPtr)memoryHandle.Pointer;
            int length = fontData.Length;

            // Create blob with pinned data (release action will unpin memory)
            var blob = new Blob(pointer, length, MemoryMode.ReadOnly, () => memoryHandle.Dispose());

            // Create face from blob
            var face = new Face(blob, 0); // 0 = first face in font file
            face.MakeImmutable();

            if (face.GlyphCount == 0)
            {
                face.Dispose();
                _logger.LogWarning("Warning: Font {BaseFont} has invalid font data (no glyphs)", pdfFont.BaseFont);
                return null;
            }

            return face;
        }
    }
}
