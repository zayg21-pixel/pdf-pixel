using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
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
    private readonly ILogger<PdfPageCache> _logger;
    private readonly Dictionary<PdfString, PdfPattern> _patternsByName = [];
    private readonly Dictionary<PdfString, PdfGraphicsStateParameters> _graphicsStateParametersByName = [];
    private readonly PdfDictionary? _fontDictionary;
    private readonly PdfDictionary? _patternDictionary;
    private readonly PdfDictionary? _extGStateDictionary;
    private readonly PdfDictionary? _xObjectDictionary;

    public PdfPageCache(IPdfPageInternal page, IPdfDocumentInternal document, PdfDictionary resources)
    {
        _page = page;
        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfPageCache>();
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
    /// Retrieve an XObject by resource name from /XObject dictionary. Returns null if not found.
    /// </summary>
    public PdfXObject? GetXObject(in PdfString xObjectName)
    {
        PdfObject? pageObject = _xObjectDictionary?.GetObject(xObjectName);

        if (pageObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' could not be resolved.", xObjectName);
            return null;
        }

        return PdfXObject.FromObject(pageObject);
    }

    /// <summary>
    /// Get (and cache) a font by resource name. Returns null if not found or cannot be created.
    /// </summary>
    public PdfFontBase? GetFont(in PdfString fontName)
    {
        if (_fontDictionary == null)
        {
            _logger.LogWarning("Font '{FontName}' requested but the page has no /Font resources.", fontName);
            return null;
        }

        PdfObject? fontObject = _fontDictionary.GetObject(fontName);
        if (fontObject == null)
        {
            _logger.LogWarning("Font '{FontName}' is not present in the page's /Font resources.", fontName);
            return null;
        }

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
        if (newFont == null)
        {
            _logger.LogWarning("Font object {Reference} could not be created from its dictionary.", fontObject.Reference);
        }
        else if (fontObject.Reference.IsValid)
        {
            _document.ObjectCache.Fonts[fontObject.Reference] = newFont;
        }

        return newFont;
    }

    /// <summary>
    /// Get (and cache) a pattern by resource name from /Pattern dictionary. Returns null if not found or unsupported.
    /// Checks document-level pattern cache first when indirect reference is present.
    /// </summary>
    public PdfPattern? GetPattern(IPdfRenderer renderer, in PdfString patternName)
    {
        if (_patternsByName.TryGetValue(patternName, out PdfPattern? cachedPattern))
        {
            return cachedPattern;
        }

        if (_patternDictionary == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' requested but the page has no /Pattern resources.", patternName);
            return null;
        }

        PdfObject? patternObject = _patternDictionary.GetObject(patternName);

        if (patternObject == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' is not present in the page's /Pattern resources.", patternName);
            return null;
        }

        PdfPattern? parsedPattern = PdfPatternParser.ParsePattern(renderer, patternObject, _page);

        if (parsedPattern == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' could not be parsed.", patternName);
        }
        else
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
        if (processor == null || graphicsState == null)
        {
            return;
        }

        if (_extGStateDictionary == null)
        {
            _logger.LogWarning("ExtGState '{GraphicsStateName}' requested but the page has no /ExtGState resources.", graphicsStateName);
            return;
        }

        if (!_graphicsStateParametersByName.TryGetValue(graphicsStateName, out PdfGraphicsStateParameters? parameters))
        {
            PdfDictionary? gsDict = _extGStateDictionary.GetDictionary(graphicsStateName);
            if (gsDict == null)
            {
                _logger.LogWarning("ExtGState '{GraphicsStateName}' is not present in the page's /ExtGState resources.", graphicsStateName);
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
