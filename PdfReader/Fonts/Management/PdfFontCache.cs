using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Default implementation of font cache.
    /// </summary>
    internal class PdfFontCache : IFontCache // TODO: We can potentially remove this cache and store in fonts directly.
    {
        private readonly ConcurrentDictionary<PdfFontBase, SKTypeface> _typefaceCache = new ConcurrentDictionary<PdfFontBase, SKTypeface>();
        private readonly ConcurrentDictionary<PdfReference, CffNameKeyedInfo> _ccfMaps = new ConcurrentDictionary<PdfReference, CffNameKeyedInfo>();
        private readonly ConcurrentDictionary<PdfFontBase, IByteCodeToGidMapper> _byteCodeToGidMapperCache = new ConcurrentDictionary<PdfFontBase, IByteCodeToGidMapper>();
        private readonly PdfDocument _document;
        private readonly ILogger<PdfFontCache> _logger;
        private readonly CffSidGidMapper _cffMapper;
        private bool _disposed = false;

        public PdfFontCache(PdfDocument document)
        {
            _document = document;
            _logger = document.LoggerFactory.CreateLogger<PdfFontCache>();
            _cffMapper = new CffSidGidMapper(document);
        }

        /// <summary>
        /// Get or create a SKTypeface for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get typeface for</param>
        /// <returns>SKTypeface instance</returns>
        public SKTypeface GetTypeface(PdfFontBase font)
        {
            if (font == null)
            {
                return SKTypeface.Default;
            }

            if (!_typefaceCache.TryGetValue(font, out var cachedTypeface))
            {
                SKTypeface typeface = null;
                var descriptor = font.FontDescriptor;
                if (descriptor != null && descriptor.FontFileObject != null)
                {
                    // Embedded font: load from stream
                    using var stream = DecodeFontStream(descriptor);
                    if (stream != null)
                    {
                        typeface = SKTypeface.FromStream(stream);
                    }
                }
                if (typeface == null)
                {
                    // Non-embedded font: use SkiaFontLoader for substitution
                    typeface = SkiaFontSubstitutor.SubstituteTypeface(font.BaseFont, descriptor);
                }
                _typefaceCache[font] = typeface ?? SKTypeface.Default;
                cachedTypeface = _typefaceCache[font];
            }

            return cachedTypeface;
        }

        /// <summary>
        /// Gets a glyph name to GID mapper for the specified font.
        /// Resolves a CFF mapper for CFF fonts, or an SFNT mapper for TrueType/OpenType fonts.
        /// Returns null for unsupported or non-TrueType font types.
        /// Caches the mapper for future calls.
        /// </summary>
        /// <param name="font">The PDF font to get the mapper for.</param>
        /// <returns>An IByteCodeToGidMapper for the font, or null if not available or not a TrueType/CFF font.</returns>
        public IByteCodeToGidMapper GetByteCodeToGidMapper(PdfFontBase font)
        {
            if (font == null)
            {
                return null;
            }

            if (_byteCodeToGidMapperCache.TryGetValue(font, out var cachedMapper))
            {
                return cachedMapper;
            }

            IByteCodeToGidMapper mapper = null;

            // CFF font (Type1C or CIDFontType0C) requires descriptor
            var descriptor = font.FontDescriptor;
            var flags = descriptor?.Flags ?? PdfFontFlags.None;

            if (descriptor != null && (descriptor.FontFileFormat == PdfFontFileFormat.Type1C || descriptor.FontFileFormat == PdfFontFileFormat.CIDFontType0C))
            {
                var cffInfo = GetCffInfo(descriptor);
                if (cffInfo != null)
                {
                    mapper = new CffByteCodeToGidMapper(cffInfo, flags, font.Encoding, font.Differences);
                }
            }
            else if (font is PdfSimpleFont simpleFont)
            {
                var typeface = GetTypeface(font);
                if (typeface != null)
                {
                    mapper = new SntfByteCodeToGidMapper(typeface, flags, simpleFont.Encoding, simpleFont.Differences, simpleFont.ToUnicodeCMap);
                }
            }

            if (mapper != null)
            {
                _byteCodeToGidMapperCache[font] = mapper;
            }

            return mapper;
        }

        private Stream DecodeFontStream(PdfFontDescriptor decriptor)
        {
            try
            {
                var cffInfo = GetCffInfo(decriptor);

                if (cffInfo != null)
                {
                    var result = CffOpenTypeWrapper.Wrap(decriptor, cffInfo);
                    return new MemoryStream(result);
                }
                else
                {
                    return _document.StreamDecoder.DecodeContentAsStream(decriptor.FontFileObject);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private CffNameKeyedInfo GetCffInfo(PdfFontDescriptor descriptor)
        {
            if (descriptor?.FontFileObject == null)
            {
                return null;
            }

            return _ccfMaps.GetOrAdd(descriptor.FontFileObject.Reference, _ => DecodeCffInfo(descriptor));
        }

        private CffNameKeyedInfo DecodeCffInfo(PdfFontDescriptor decriptor)
        {
            try
            {
                if (decriptor.HasEmbeddedFont && decriptor.FontFileFormat == PdfFontFileFormat.Type1C || decriptor.FontFileFormat == PdfFontFileFormat.CIDFontType0C)
                {
                    var decoded = _document.StreamDecoder.DecodeContentStream(decriptor.FontFileObject);

                    if (_cffMapper.TryParseNameKeyed(decoded, out var cffInfo))
                    {
                        return cffInfo;
                    }
                }
            }
            catch
            {
                // Swallow exceptions - higher level code can fallback.
            }

            return null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var typeface in _typefaceCache.Values)
                    {
                        typeface?.Dispose();
                    }

                    _typefaceCache.Clear();
                    _byteCodeToGidMapperCache.Clear();
                    _ccfMaps.Clear();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}