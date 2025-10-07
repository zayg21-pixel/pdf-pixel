using System;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Loads and caches reusable PDF resources (fonts, CMaps) for a document instance.
    /// Instance-based to provide structured logging.
    /// </summary>
    public class PdfResourceLoader
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfResourceLoader> _logger;

        /// <summary>
        /// Creates a new resource loader bound to a specific PDF document.
        /// </summary>
        /// <param name="document">Target <see cref="PdfDocument"/> whose resources will be populated.</param>
        public PdfResourceLoader(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = _document.LoggerFactory.CreateLogger<PdfResourceLoader>();
        }

        /// <summary>
        /// Load and cache page level resources (fonts and CMaps) discovered in the document object table.
        /// Failures for individual objects are logged as warnings and do not abort the scan.
        /// </summary>
        public void LoadPageResources()
        {
            var loadedFonts = _document.Fonts;
            var loadedCMaps = _document.CMaps;

            foreach (var pdfObject in _document.Objects.Values)
            {
                try
                {
                    if (PdfFontFactory.IsFontObject(pdfObject))
                    {
                        var fontReference = pdfObject.Reference;
                        if (!loadedFonts.ContainsKey(fontReference))
                        {
                            var font = PdfFontFactory.CreateFont(pdfObject);
                            if (font != null)
                            {
                                loadedFonts[fontReference] = font;
                            }
                        }
                    }

                    var typeName = pdfObject.Dictionary?.GetName(PdfTokens.TypeKey);
                    if (!string.IsNullOrEmpty(typeName) && string.Equals(typeName, PdfTokens.CMapTypeValue, StringComparison.Ordinal))
                    {
                        var cmapName = pdfObject.Dictionary.GetName(PdfTokens.CMapNameKey);
                        if (!string.IsNullOrEmpty(cmapName) && !loadedCMaps.ContainsKey(cmapName))
                        {
                            var decodedData = PdfStreamDecoder.DecodeContentStream(pdfObject);
                            if (!decodedData.IsEmpty && decodedData.Length > 0)
                            {
                                var cmapContext = new PdfParseContext(decodedData);
                                var parsedCMap = PdfToUnicodeCMapParser.ParseCMapFromContext(ref cmapContext, _document);
                                if (parsedCMap != null)
                                {
                                    loadedCMaps[cmapName] = parsedCMap;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed loading resource from object {ObjectNumber}", pdfObject?.Reference.ObjectNumber);
                }
            }
        }
    }
}