using System;
using System.Collections.Generic;
using PdfReader.Fonts.Types;
using PdfReader.Fonts;
using SkiaSharp;
using PdfReader.Rendering.State;
using PdfReader.Color.ColorSpace;
using PdfReader.Pattern.Model;
using PdfReader.Pattern.Utilities;
using PdfReader.Text;

namespace PdfReader.Models
{
    /// <summary>
    /// Per-page name-based resource cache to avoid repeated dictionary lookups and conversions.
    /// Caches fonts, patterns, and color spaces by their resource name (e.g. /F1, /P1, /CS1).
    /// Underlying PdfDocument still caches referenced resources by indirect object reference.
    /// Implements <see cref="IDisposable"/> to release any disposable cached pattern instances.
    /// </summary>
    internal sealed class PdfPageCache : IDisposable
    {
        private readonly PdfPage _page;
        private readonly Dictionary<PdfString, PdfFontBase> _fontsByName = new Dictionary<PdfString, PdfFontBase>();
        private readonly Dictionary<PdfString, PdfPattern> _patternsByName = new Dictionary<PdfString, PdfPattern>();
        private readonly Dictionary<PdfString, PdfColorSpaceConverter> _colorSpacesByName = new Dictionary<PdfString, PdfColorSpaceConverter>();
        private readonly Dictionary<PdfString, PdfGraphicsStateParameters> _graphicsStateParametersByName = new Dictionary<PdfString, PdfGraphicsStateParameters>();
        private readonly PdfDictionary _fontDictionary; // captured once
        private readonly PdfDictionary _patternDictionary; // captured once
        private readonly PdfDictionary _extGStateDictionary; // captured once
        private readonly PdfDictionary _xObjectDictionary;
        private bool _disposed;

        public PdfPageCache(PdfPage page)
        {
            ColorSpace = new ColorSpaceResolver(page);
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _fontDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.FontKey);
            _patternDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.PatternKey);
            _extGStateDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.ExtGStateKey);
            _xObjectDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.XObjectKey);
        }

        /// <summary>
        /// Gets the resolver used to determine the color space for image processing operations.
        /// </summary>
        public ColorSpaceResolver ColorSpace { get; }

        /// <summary>
        /// Retrieve an XObject by resource name from /XObject dictionary. Returns null if not found.
        /// </summary>
        public PdfXObject GetXObject(PdfString xObjectName)
        {
            var pageObject = _xObjectDictionary.GetObject(xObjectName);

            if (pageObject == null)
            {
                return null;
            }

            return PdfXObject.FromObject(pageObject);
        }

        /// <summary>
        /// Get (and cache) a font by resource name. Returns null if not found or cannot be created.
        /// </summary>
        public PdfFontBase GetFont(PdfString fontName)
        {
            if (fontName.IsEmpty)
            {
                return null;
            }
            if (_fontsByName.TryGetValue(fontName, out var cached))
            {
                return cached;
            }
            if (_fontDictionary == null)
            {
                return null;
            }
            var fontReference = _fontDictionary.GetReference(fontName);
            if (fontReference.IsValid && _page.Document.Fonts.TryGetValue(fontReference, out var documentCachedFont))
            {
                _fontsByName[fontName] = documentCachedFont;
                return documentCachedFont;
            }
            var fontObjectDictionary = _fontDictionary.GetDictionary(fontName);
            if (fontObjectDictionary == null)
            {
                return null;
            }
            var newFont = PdfFontFactory.CreateFont(fontObjectDictionary);
            if (newFont != null)
            {
                if (fontReference.IsValid)
                {
                    _page.Document.Fonts[fontReference] = newFont;
                }
                _fontsByName[fontName] = newFont;
            }
            return newFont;
        }

        /// <summary>
        /// Get (and cache) a pattern by resource name from /Pattern dictionary. Returns null if not found or unsupported.
        /// Checks document-level pattern cache first when indirect reference is present.
        /// </summary>
        public PdfPattern GetPattern(PdfString patternName)
        {
            if (patternName.IsEmpty)
            {
                return null;
            }
            if (_patternsByName.TryGetValue(patternName, out var cachedPattern))
            {
                return cachedPattern;
            }
            if (_patternDictionary == null)
            {
                return null;
            }
            var patternReference = _patternDictionary.GetReference(patternName);
            PdfObject patternObject = null;
            if (patternReference.IsValid)
            {
                patternObject = _page.Document.GetObject(patternReference);
            }
            else
            {
                var inlinePatternDictionary = _patternDictionary.GetDictionary(patternName);
                if (inlinePatternDictionary != null)
                {
                    patternObject = new PdfObject(new PdfReference(-1,0), _page.Document, PdfValue.Dictionary(inlinePatternDictionary));
                }
            }
            if (patternObject == null)
            {
                return null;
            }
            var parsedPattern = PdfPatternParser.ParsePattern(patternObject, _page);
            if (parsedPattern != null)
            {
                _patternsByName[patternName] = parsedPattern;
            }
            return parsedPattern;
        }

        /// <summary>
        /// Apply graphics state parameters from an ExtGState entry identified by name.
        /// Parses and caches the parameters; applies them to the graphicsState and concatenates any transform matrix.
        /// </summary>
        /// <param name="graphicsStateName">Name of the ExtGState resource (/GS)</param>
        /// <param name="canvas">Target canvas for matrix concatenation</param>
        /// <param name="graphicsState">Graphics state to update</param>
        internal void ApplyGraphicsStateParameters(PdfString graphicsStateName, SKCanvas canvas, PdfGraphicsState graphicsState)
        {
            if (graphicsStateName.IsEmpty)
            {
                return;
            }
            if (canvas == null || graphicsState == null)
            {
                return;
            }
            if (_extGStateDictionary == null)
            {
                return;
            }
            if (!_graphicsStateParametersByName.TryGetValue(graphicsStateName, out var parameters))
            {
                var gsDict = _extGStateDictionary.GetDictionary(graphicsStateName);
                if (gsDict == null)
                {
                    return;
                }
                parameters = PdfGraphicsStateParser.ParseGraphicsStateParametersFromDictionary(gsDict, _page);
                _graphicsStateParametersByName[graphicsStateName] = parameters;
            }
            parameters.ApplyToGraphicsState(graphicsState);
            if (parameters.TransformMatrix.HasValue)
            {
                canvas.Concat(parameters.TransformMatrix.Value);
            }
        }

        /// <summary>
        /// Release cached entries without disposing the cache object itself.
        /// Disposes disposable pattern instances and clears local dictionaries.
        /// Intended for manual cache flush scenarios (e.g. memory pressure) while keeping the instance usable.
        /// </summary>
        public void ReleaseCache()
        {
            foreach (var patternEntry in _patternsByName)
            {
                patternEntry.Value?.Dispose();
            }

            _patternsByName.Clear();
            _fontsByName.Clear();
            _colorSpacesByName.Clear();
            _graphicsStateParametersByName.Clear();
        }

        /// <summary>
        /// Dispose cached disposable resources (patterns). Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            foreach (var patternEntry in _patternsByName)
            {
                patternEntry.Value?.Dispose();
            }

            _patternsByName.Clear();
            _fontsByName.Clear();
            _colorSpacesByName.Clear();
            _graphicsStateParametersByName.Clear();
            _disposed = true;
        }
    }
}
