using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Per-page name-based resource cache to avoid repeated dictionary lookups and conversions.
/// Caches fonts, patterns, and color spaces by their resource name (e.g. /F1, /P1, /CS1).
/// Underlying PdfDocument still caches referenced resources by indirect object reference.
/// </summary>
internal sealed class PdfPageCache
{
    private readonly IPdfPageInternal _page;
    private readonly IPdfDocumentInternal _document;
    private readonly Dictionary<PdfString, PdfPattern> _patternsByName = [];
    private readonly Dictionary<PdfString, PdfGraphicsStateParameters> _graphicsStateParametersByName = [];
    private readonly PdfDictionary? _fontDictionary; // captured once
    private readonly PdfDictionary? _patternDictionary; // captured once
    private readonly PdfDictionary? _extGStateDictionary; // captured once
    private readonly PdfDictionary? _xObjectDictionary;

    public PdfPageCache(IPdfPageInternal page, IPdfDocumentInternal document, PdfDictionary resources)
    {
        _page = page;
        _document = document;
        ColorSpace = new ColorSpaceResolver(document, resources);
        _fontDictionary = resources.GetDictionary(PdfTokens.FontKey);
        _patternDictionary = resources.GetDictionary(PdfTokens.PatternKey);
        _extGStateDictionary = resources.GetDictionary(PdfTokens.ExtGStateKey);
        _xObjectDictionary = resources.GetDictionary(PdfTokens.XObjectKey);
    }

    /// <summary>
    /// Gets the resolver used to determine the color space for image processing operations.
    /// </summary>
    public ColorSpaceResolver ColorSpace { get; }

    /// <summary>
    /// Releases name-based lookup caches (patterns, graphics state parameters) that are only
    /// needed during content stream recording. ColorSpace resolver is retained for image decoders.
    /// </summary>
    public void ClearAfterRender()
    {
        _patternsByName.Clear();
        _graphicsStateParametersByName.Clear();
    }

    /// <summary>
    /// Retrieve an XObject by resource name from /XObject dictionary. Returns null if not found.
    /// </summary>
    public PdfXObject? GetXObject(in PdfString xObjectName)
    {
        PdfObject? pageObject = _xObjectDictionary?.GetObject(xObjectName);

        if (pageObject == null)
        {
            return null;
        }

        return PdfXObject.FromObject(pageObject);
    }

    /// <summary>
    /// Get (and cache) a font by resource name. Returns null if not found or cannot be created.
    /// </summary>
    public PdfFontBase? GetFont(in PdfString fontName)
    {
        if (fontName.IsEmpty)
        {
            return null;
        }

        if (_fontDictionary == null)
        {
            return null;
        }

        PdfObject? fontObject = _fontDictionary.GetObject(fontName);
        return GetFont(fontObject);
    }

    /// <summary>
    /// Get (and cache) a font from a PdfObject. Returns null if not found or cannot be created.
    /// </summary>
    /// <param name="fontObject">Font object.</param>
    /// <returns></returns>
    public PdfFontBase? GetFont(PdfObject? fontObject)
    {
        if (fontObject == null)
        {
            return null;
        }

        if (fontObject.Reference.IsValid && _document.ObjectCache.Fonts.TryGetValue(fontObject.Reference, out PdfFontBase? documentCachedFont))
        {
            return documentCachedFont;
        }

        PdfFontBase? newFont = PdfFontFactory.CreateFont(fontObject);
        if (newFont != null)
        {
            if (fontObject.Reference.IsValid)
            {
                _document.ObjectCache.Fonts[fontObject.Reference] = newFont;
            }
        }

        return newFont;
    }

    /// <summary>
    /// Get (and cache) a pattern by resource name from /Pattern dictionary. Returns null if not found or unsupported.
    /// Checks document-level pattern cache first when indirect reference is present.
    /// </summary>
    public PdfPattern? GetPattern(IPdfRenderer renderer, in PdfString patternName)
    {
        if (patternName.IsEmpty)
        {
            return null;
        }

        if (_patternsByName.TryGetValue(patternName, out PdfPattern? cachedPattern))
        {
            return cachedPattern;
        }

        if (_patternDictionary == null)
        {
            return null;
        }

        PdfObject? patternObject = _patternDictionary.GetObject(patternName);

        if (patternObject == null)
        {
            return null;
        }

        PdfPattern? parsedPattern = PdfPatternParser.ParsePattern(renderer, patternObject);

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
    /// <param name="processor">Command processor for matrix concatenation</param>
    /// <param name="graphicsState">Graphics state to update</param>
    internal void ApplyGraphicsStateParameters(in PdfString graphicsStateName, IPdfCommandProcessor processor, PdfGraphicsState graphicsState)
    {
        if (graphicsStateName.IsEmpty)
        {
            return;
        }

        if (processor == null || graphicsState == null)
        {
            return;
        }

        if (_extGStateDictionary == null)
        {
            return;
        }

        if (!_graphicsStateParametersByName.TryGetValue(graphicsStateName, out PdfGraphicsStateParameters? parameters))
        {
            PdfDictionary? gsDict = _extGStateDictionary.GetDictionary(graphicsStateName);
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
            processor.Process(new ConcatMatrixCommand(parameters.TransformMatrix.Value));
        }
    }
}
