using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
using PdfReader.Parsing;
using PdfReader.Streams;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PdfReader.Color.ColorSpace;
using PdfReader.Color.Icc.Model;
using PdfReader.Functions;

namespace PdfReader.Models
{
    /// <summary>
    /// Represents a parsed PDF document with object table, pages, resources and renderer.
    /// Adds lazy object resolution support via an object index.
    /// </summary>
    public class PdfDocument : IDisposable
    {
        private readonly Dictionary<PdfReference, PdfObject> _objects = new Dictionary<PdfReference, PdfObject>();
        private readonly PdfObjectParser _pdfObjectParser;

        public PdfDocument(ILoggerFactory loggerFactory, Stream fileStream)
        {
            LoggerFactory = loggerFactory;
            StreamDecoder = new PdfStreamDecoder(this);
            FontCache = new PdfFontCache(this);
            _pdfObjectParser = new PdfObjectParser(this);
            Stream = new BufferedStream(fileStream);
        }

        internal ILoggerFactory LoggerFactory { get; }

        internal PdfFontCache FontCache { get; }

        /// <summary>
        /// Local page font cache.
        /// </summary>
        internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = new Dictionary<PdfReference, PdfFontBase>();

        /// <summary>
        /// Local page color space converter cache.
        /// </summary>
        internal Dictionary<PdfReference, PdfColorSpaceConverter> ColorSpaceConverters { get; } = new Dictionary<PdfReference, PdfColorSpaceConverter>();

        public PdfCMap GetCmap(PdfString name)
        {
            return null;
            // TODO: Implement standard CMap retrieval (predefined and embedded)
        }

        /// <summary>
        /// High-level cache for parsed PDF functions, keyed by reference.
        /// </summary>
        internal ConcurrentDictionary<PdfReference, PdfFunction> FunctionObjectCache { get; } = new ConcurrentDictionary<PdfReference, PdfFunction>();

        internal PdfStreamDecoder StreamDecoder { get; }

        public List<PdfPage> Pages { get; } = new List<PdfPage>();

        public int PageCount => Pages.Count;

        public PdfObject RootObject { get; set; }

        public BasePdfDecryptor Decryptor { get; internal set; }

        internal Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; } = new Dictionary<PdfReference, PdfObjectInfo>();

        /// <summary>
        /// Exposes the original PDF file bytes for internal parser use (lazy object loading).
        /// </summary>
        internal BufferedStream Stream { get; }

        /// <summary>
        /// Parsed catalog output intent ICC profile (first preferred or first valid). Null when none present or invalid.
        /// Populated by <see cref="Parsing.PdfOutputIntentParser"/> post xref/catalog load.
        /// </summary>
        internal IccProfile OutputIntentProfile { get; set; }

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
                _objects[parsed.Reference] = parsed;
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

            ColorSpaceConverters.Clear();

            Stream.Dispose();
        }
    }
}