using System;
using PdfReader.Fonts;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Simplified resource loader that caches fonts and CMaps for quick reuse
    /// </summary>
    public static class PdfResourceLoader
    {
        /// <summary>
        /// Load and cache page resources (fonts and CMaps) from the document
        /// </summary>
        public static void LoadPageResources(PdfDocument document)
        {
            var loadedFonts = document.Fonts;
            var loadedCMaps = document.CMaps;
            
            // Scan all objects; cache fonts and CMaps
            foreach (var obj in document.Objects.Values)
            {
                try
                {
                    // Fonts
                    if (PdfFontFactory.IsFontObject(obj))
                    {
                        var fontRef = obj.Reference;
                        if (!loadedFonts.ContainsKey(fontRef))
                        {
                            var font = PdfFontFactory.CreateFont(obj);
                            if (font != null)
                            {
                                loadedFonts[fontRef] = font;
                            }
                        }
                    }

                    // CMaps: detect /Type /CMap and cache by /CMapName if present
                    var typeName = obj.Dictionary?.GetName(PdfTokens.TypeKey);
                    if (!string.IsNullOrEmpty(typeName) && string.Equals(typeName, PdfTokens.CMapTypeValue, StringComparison.Ordinal))
                    {
                        var cmapName = obj.Dictionary.GetName(PdfTokens.CMapNameKey);
                        if (!string.IsNullOrEmpty(cmapName) && !loadedCMaps.ContainsKey(cmapName))
                        {
                            var data = PdfStreamDecoder.DecodeContentStream(obj);
                            if (!data.IsEmpty && data.Length > 0)
                            {
                                var ctx = new PdfParseContext(data);
                                var parsed = PdfToUnicodeCMapParser.ParseCMapFromContext(ref ctx, document);
                                if (parsed != null)
                                {
                                    loadedCMaps[cmapName] = parsed;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore individual failures to keep scanning
                }
            }
        }
    }
}