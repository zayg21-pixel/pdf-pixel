using PdfReader.Fonts;
using PdfReader.Rendering;
using PdfReader.Rendering.Color;
using System;
using System.Collections.Generic;

namespace PdfReader.Models
{
    public class PdfDocument : IDisposable
    {
        public PdfDocument()
        {
            PdfRenderer = new PdfRenderer(FontCache);
        }

        internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = new Dictionary<PdfReference, PdfFontBase>();

        internal Dictionary<PdfReference, PdfColorSpaceConverter> ColorSpaceConverters { get; } = new Dictionary<PdfReference, PdfColorSpaceConverter>();

        internal PdfFontCache FontCache { get; } = new PdfFontCache();

        /// <summary>
        /// Cache of parsed CMaps keyed by /CMapName. Populated by resource loader for reuse (e.g., usecmap resolution).
        /// </summary>
        internal Dictionary<string, PdfToUnicodeCMap> CMaps { get; } = new Dictionary<string, PdfToUnicodeCMap>(StringComparer.Ordinal);

        public Dictionary<int, PdfObject> Objects { get; set; } = new Dictionary<int, PdfObject>();
        public List<PdfPage> Pages { get; set; } = new List<PdfPage>();
        public int PageCount { get; set; }
        public int RootRef { get; set; }

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