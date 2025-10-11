using HarfBuzzSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Cff;
using PdfReader.Models;
using PdfReader.Streams;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace PdfReader.Fonts.Management
{
    /// <summary>
    /// Default implementation of font cache that contains both SKTypeface and HarfBuzz fonts
    /// Updated to use PdfFontBase hierarchy as dictionary key for efficient lookups
    /// </summary>
    internal class PdfFontCache : IFontCache
    {
        private readonly Dictionary<PdfFontBase, SKTypeface> _typefaceCache = new Dictionary<PdfFontBase, SKTypeface>();
        private readonly Dictionary<PdfFontBase, Font> _harfBuzzFontCache = new Dictionary<PdfFontBase, Font>();
        private readonly Dictionary<(int weight, int width, SKFontStyleSlant slant), SKTypeface> _fallbackCache = new Dictionary<(int weight, int width, SKFontStyleSlant slant), SKTypeface>();
        private readonly ConcurrentDictionary<PdfReference, CffNameKeyedInfo> _ccfMaps = new ConcurrentDictionary<PdfReference, CffNameKeyedInfo>();
        private readonly PdfDocument _document;
        private readonly ILogger<PdfFontCache> _logger;
        private readonly SkiaFontLoader _skiaFontLoader;
        private readonly CffSidGidMapper _cffMapper;

        private bool _disposed = false;

        public PdfFontCache(PdfDocument document)
        {
            _document = document;
            _skiaFontLoader = new SkiaFontLoader(document, this);
            _logger = document.LoggerFactory.CreateLogger<PdfFontCache>();
            _cffMapper = new CffSidGidMapper(document);
        }

        public Stream GetFontStream(PdfFontDescriptor decriptor)
        {
            if (decriptor?.FontFileObject == null)
            {
                return null;
            }

            return DecodeFontStream(decriptor);
        }

        public CffNameKeyedInfo GetCffInfo(PdfFontDescriptor decriptor)
        {
            if (decriptor?.FontFileObject == null)
            {
                return null;
            }

            if (_ccfMaps.TryGetValue(decriptor.FontFileObject.Reference, out var cached))
            {
                return cached;
            }

            return _ccfMaps.GetOrAdd(decriptor.FontFileObject.Reference, _ => DecodeCffInfo(decriptor));
        }

        private CffNameKeyedInfo DecodeCffInfo(PdfFontDescriptor decriptor)
        {
            try
            {
                if (decriptor.HasEmbeddedFont && decriptor.FontFileFormat == FontFileFormat.Type1C || decriptor.FontFileFormat == FontFileFormat.CIDFontType0C)
                {
                    var decoded = PdfStreamDecoder.DecodeContentStream(decriptor.FontFileObject);

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
                    return PdfStreamDecoder.DecodeContentAsStream(decriptor.FontFileObject);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get or create a SKTypeface for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get typeface for</param>
        /// <returns>SKTypeface instance</returns>
        public SKTypeface GetTypeface(PdfFontBase font)
        {
            if (font == null) return SKTypeface.Default;

            if (!_typefaceCache.TryGetValue(font, out var cachedTypeface))
            {
                cachedTypeface = _skiaFontLoader.GetTypeface(font);
                _typefaceCache[font] = cachedTypeface;
            }

            return cachedTypeface;
        }

        public SKTypeface GetFallbackFromParameters(int weight, int width, SKFontStyleSlant slant)
        {
            var key = (weight, width, slant);
            if (!_fallbackCache.TryGetValue(key, out var cachedTypeface))
            {
                cachedTypeface = SKTypeface.FromFamilyName("Arial", weight, width, slant);
                _fallbackCache[key] = cachedTypeface;
            }

            return cachedTypeface;
        }

        /// <summary>
        /// Clear all cached fonts
        /// </summary>
        public void ClearCache()
        {
            foreach (var typeface in _typefaceCache.Values)
            {
                typeface?.Dispose();
            }
            _typefaceCache.Clear();

            foreach (var font in _harfBuzzFontCache.Values)
            {
                font?.Dispose();
            }

            foreach (var fallback in _fallbackCache.Values)
            {
                fallback?.Dispose();
            }

            _typefaceCache.Clear();
            _fallbackCache.Clear();
            _harfBuzzFontCache.Clear();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ClearCache();
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