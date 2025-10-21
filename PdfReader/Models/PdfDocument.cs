using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Fonts;
using PdfReader.Rendering;
using PdfReader.Rendering.Color;
using PdfReader.Parsing;
using System;
using System.Collections.Generic;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Mapping;
using PdfReader.Streams;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents a parsed PDF document with object table, pages, resources and renderer.
    /// Adds lazy object resolution support via an object index.
    /// Existing eager parsing continues to populate the internal cache; callers should prefer <see cref="GetObject"/>.
    /// </summary>
    public class PdfDocument : IDisposable
    {
        private readonly Dictionary<PdfReference, PdfObject> _objects = new Dictionary<PdfReference, PdfObject>();
        private readonly PdfObjectParser _pdfObjectParser;

        private readonly ReadOnlyMemory<byte> _fileBytes;

        public PdfDocument(ILoggerFactory loggerFactory, ReadOnlyMemory<byte> fileBytes)
        {
            LoggerFactory = loggerFactory;
            StreamDecoder = new PdfStreamDecoder(this);
            FontCache = new PdfFontCache(this);
            PdfRenderer = new PdfRenderer(FontCache, loggerFactory);
            _fileBytes = fileBytes;
            _pdfObjectParser = new PdfObjectParser(this);
        }

        internal ILoggerFactory LoggerFactory { get; }

        internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = new Dictionary<PdfReference, PdfFontBase>();

        internal Dictionary<PdfReference, PdfColorSpaceConverter> ColorSpaceConverters { get; } = new Dictionary<PdfReference, PdfColorSpaceConverter>();

        internal PdfFontCache FontCache { get; }

        internal Dictionary<string, PdfToUnicodeCMap> CMaps { get; } = new Dictionary<string, PdfToUnicodeCMap>(StringComparer.Ordinal);

        // TODO: remove, implement more high-level function caching
        internal Dictionary<PdfReference, PdfFunctionCacheEntry> FunctionCache { get; } = new Dictionary<PdfReference, PdfFunctionCacheEntry>();

        internal PdfStreamDecoder StreamDecoder { get; }

        public List<PdfPage> Pages { get; set; } = new List<PdfPage>();

        public int PageCount { get; set; }

        public PdfObject RootObject { get; set; }

        public BasePdfDecryptor Decryptor { get; internal set; }

        internal Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; } = new Dictionary<PdfReference, PdfObjectInfo>();

        public PdfRenderer PdfRenderer { get; }

        /// <summary>
        /// Exposes the original PDF file bytes for internal parser use (lazy object loading).
        /// </summary>
        internal ReadOnlyMemory<byte> FileBytes => _fileBytes;

        internal void StoreParsedObject(PdfObject pdfObject)
        {
            if (pdfObject == null)
            {
                return;
            }
            _objects[pdfObject.Reference] = pdfObject;
        }

        /// <summary>
        /// Retrieve an object by reference, parsing it lazily if present in the index but not yet materialized.
        /// Only uncompressed indexed objects are currently supported in the lazy path.
        /// </summary>
        /// <param name="reference">Target object reference.</param>
        /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
        public PdfObject GetObject(PdfReference reference)
        {
            if (!reference.IsValid)
            {
                return null;
            }

            if (_objects.TryGetValue(reference, out var existing))
            {
                return existing;
            }

            if (!ObjectIndex.TryGetValue(reference, out var info))
            {
                return null;
            }

            var parsed = _pdfObjectParser.ParseSingleIndexedObject(info);
            if (parsed != null)
            {
                StoreParsedObject(parsed);
            }
            return parsed;
        }

        public void Dispose()
        {
            FontCache.Dispose();

            foreach (var converter in ColorSpaceConverters.Values)
            {
                converter.Dispose();
            }
        }
    }
}