using System;
using System.Collections.Generic;
using PdfRender.Fonts;
using SkiaSharp;
using PdfRender.Rendering.State;
using PdfRender.Color.ColorSpace;
using PdfRender.Pattern.Model;
using PdfRender.Pattern.Utilities;
using PdfRender.Text;
using PdfRender.Rendering;
using PdfRender.Fonts.Model;

namespace PdfRender.Models;

/// <summary>
/// Per-page name-based resource cache to avoid repeated dictionary lookups and conversions.
/// Caches fonts, patterns, and color spaces by their resource name (e.g. /F1, /P1, /CS1).
/// Underlying PdfDocument still caches referenced resources by indirect object reference.
/// </summary>
internal sealed class PdfPageCache
{
    private readonly PdfPage _page;
    private readonly Dictionary<PdfString, PdfPattern> _patternsByName = new Dictionary<PdfString, PdfPattern>();
    private readonly Dictionary<PdfString, PdfGraphicsStateParameters> _graphicsStateParametersByName = new Dictionary<PdfString, PdfGraphicsStateParameters>();
    private readonly PdfDictionary _fontDictionary; // captured once
    private readonly PdfDictionary _patternDictionary; // captured once
    private readonly PdfDictionary _extGStateDictionary; // captured once
    private readonly PdfDictionary _xObjectDictionary;

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

        if (_fontDictionary == null)
        {
            return null;
        }

        var fontObject = _fontDictionary.GetObject(fontName);
        return GetFont(fontObject);
    }

    /// <summary>
    /// Get (and cache) a font from a PdfObject. Returns null if not found or cannot be created.
    /// </summary>
    /// <param name="fontObject">Font object.</param>
    /// <returns></returns>
    public PdfFontBase GetFont(PdfObject fontObject)
    {
        if (fontObject == null)
        {
            return null;
        }

        if (fontObject.Reference.IsValid && _page.Document.ObjectCache.Fonts.TryGetValue(fontObject.Reference, out var documentCachedFont))
        {
            return documentCachedFont;
        }
        var fontObjectDictionary = fontObject;

        var newFont = PdfFontFactory.CreateFont(fontObject);
        if (newFont != null)
        {
            if (fontObject.Reference.IsValid)
            {
                _page.Document.ObjectCache.Fonts[fontObject.Reference] = newFont;
            }
        }

        return newFont;
    }

    /// <summary>
    /// Get (and cache) a pattern by resource name from /Pattern dictionary. Returns null if not found or unsupported.
    /// Checks document-level pattern cache first when indirect reference is present.
    /// </summary>
    public PdfPattern GetPattern(IPdfRenderer renderer, PdfString patternName)
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
        PdfObject patternObject = _patternDictionary.GetObject(patternName);

        if (patternObject == null)
        {
            return null;
        }

        var parsedPattern = PdfPatternParser.ParsePattern(renderer, patternObject);

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
}
