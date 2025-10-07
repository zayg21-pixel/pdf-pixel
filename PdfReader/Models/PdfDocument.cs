using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Fonts;
using PdfReader.Rendering;
using PdfReader.Rendering.Color;
using System;
using System.Collections.Generic;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents a parsed PDF document with object table, pages, resources and renderer.
    /// </summary>
    public class PdfDocument : IDisposable
    {
        public PdfDocument(ILoggerFactory loggerFactory)
        {
            PdfRenderer = new PdfRenderer(FontCache, loggerFactory);
            LoggerFactory = loggerFactory;
        }

        internal ILoggerFactory LoggerFactory { get; }

        internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = new Dictionary<PdfReference, PdfFontBase>();

        internal Dictionary<PdfReference, PdfColorSpaceConverter> ColorSpaceConverters { get; } = new Dictionary<PdfReference, PdfColorSpaceConverter>();

        internal PdfFontCache FontCache { get; } = new PdfFontCache();

        /// <summary>
        /// Cache of parsed CMaps keyed by /CMapName. Populated by resource loader for reuse (e.g., usecmap resolution).
        /// </summary>
        internal Dictionary<string, PdfToUnicodeCMap> CMaps { get; } = new Dictionary<string, PdfToUnicodeCMap>(StringComparer.Ordinal);

        /// <summary>
        /// Cache of parsed PDF function objects (currently only Type 0 sampled functions) keyed by their reference.
        /// </summary>
        internal Dictionary<PdfReference, PdfFunctionCacheEntry> FunctionCache { get; } = new Dictionary<PdfReference, PdfFunctionCacheEntry>();

        /// <summary>
        /// Map of indirect object number to parsed object instance.
        /// </summary>
        public Dictionary<int, PdfObject> Objects { get; set; } = new Dictionary<int, PdfObject>();

        /// <summary>
        /// Logical list of pages in display order.
        /// </summary>
        public List<PdfPage> Pages { get; set; } = new List<PdfPage>();

        /// <summary>
        /// Declared or inferred page count.
        /// </summary>
        public int PageCount { get; set; }

        /// <summary>
        /// Object number of the catalog (root) dictionary.
        /// </summary>
        public int RootRef { get; set; }

        public PdfDictionary TrailerDictionary { get; internal set; }

        /// <summary>
        /// Placeholder decryptor with extracted encryption parameters if file is encrypted.
        /// </summary>
        public BasePdfDecryptor Decryptor { get; internal set; }

        /// <summary>
        /// Default renderer for PDF content streams.
        /// </summary>
        public PdfRenderer PdfRenderer { get; }

        public void Dispose()
        {
            FontCache.Dispose();
        }
    }
}