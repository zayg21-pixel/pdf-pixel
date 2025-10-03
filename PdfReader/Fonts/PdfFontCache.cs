using HarfBuzzSharp;
using PdfReader.Models;
using PdfReader.Rendering.HarfBuzz;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PdfReader.Fonts
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
        private readonly ConcurrentDictionary<PdfReference, ReadOnlyMemory<byte>> _cache = new ConcurrentDictionary<PdfReference, ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<PdfReference, CffNameKeyedInfo> _ccfMaps = new ConcurrentDictionary<PdfReference, CffNameKeyedInfo>();

        private bool _disposed = false;

        public ReadOnlyMemory<byte> GetFontStream(PdfFontDescriptor decriptor)
        {
            if (decriptor?.FontFileObject == null)
                return ReadOnlyMemory<byte>.Empty;

            // Try to get from cache first
            if (_cache.TryGetValue(decriptor.FontFileObject.Reference, out var cached))
                return cached;

            // Not in cache, decode and cache
            return _cache.GetOrAdd(decriptor.FontFileObject.Reference, _ => DecodeFontStream(decriptor));
        }

        public CffNameKeyedInfo GetCffInfo(PdfFontDescriptor decriptor)
        {
            if (decriptor?.FontFileObject == null)
                return null;
            // Try to get from cache first
            if (_ccfMaps.TryGetValue(decriptor.FontFileObject.Reference, out var cached))
                return cached;
            // Not in cache, decode and cache
            return _ccfMaps.GetOrAdd(decriptor.FontFileObject.Reference, _ => DecodeCffInfo(decriptor));
        }

        private CffNameKeyedInfo DecodeCffInfo(PdfFontDescriptor decriptor)
        {
            try
            {
                // Handle CFF-based fonts by wrapping into an OpenType container Skia can consume
                if (decriptor.HasEmbeddedFont && decriptor.FontFileFormat == FontFileFormat.Type1C || decriptor.FontFileFormat == FontFileFormat.CIDFontType0C)
                {
                    var decoded = PdfStreamDecoder.DecodeContentStream(decriptor.FontFileObject);

                    if (CffSidGidMapper.TryParseNameKeyed(decoded, out var cffInfo))
                    {
                        return cffInfo;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private ReadOnlyMemory<byte> DecodeFontStream(PdfFontDescriptor decriptor)
        {
            try
            {
                var cffInfo = GetCffInfo(decriptor);

                if (cffInfo != null)
                {
                    var result = CffOpenTypeWrapper.Wrap(decriptor, cffInfo);
                    return result;
                }
                else
                {
                    return PdfStreamDecoder.DecodeContentStream(decriptor.FontFileObject);
                }
            }
            catch (Exception)
            {
                return ReadOnlyMemory<byte>.Empty;
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
                cachedTypeface = PdfFontUtilities.GetTypeface(font);
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
        /// Get or create a HarfBuzz font for the specified PDF font
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="font">PDF font to get HarfBuzz font for</param>
        /// <returns>HarfBuzz Font instance, or null if not supported</returns>
        public Font GetHarfBuzzFont(PdfFontBase font)
        {
            if (font == null)
            {
                return null;
            }

            if (!_harfBuzzFontCache.TryGetValue(font, out var cachedFont))
            {
                cachedFont = HarfBuzzFontRenderer.CreateHarfBuzzFont(font);
                _harfBuzzFontCache[font] = cachedFont;
            }

            return cachedFont;
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