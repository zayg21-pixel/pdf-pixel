using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
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
    private readonly Dictionary<PdfString, PdfFontBase> _fontsByName = [];
    private readonly Dictionary<PdfString, PdfPattern> _patternsByName = [];
    private readonly Dictionary<PdfString, PdfGraphicsStateParameters> _graphicsStateParametersByName = [];
    private readonly Dictionary<PdfString, PdfXObject> _xObjectsByName = [];
    private readonly Dictionary<PdfString, PdfShading> _shadingsByName = [];
    private readonly Dictionary<PdfString, PdfOptionalContentMembership?> _optionalContentByName = [];
    private readonly PdfDictionary? _fontDictionary;
    private readonly PdfDictionary? _patternDictionary;
    private readonly PdfDictionary? _extGStateDictionary;
    private readonly PdfDictionary? _xObjectDictionary;
    private readonly PdfDictionary? _shadingDictionary;
    private readonly PdfDictionary? _propertiesDictionary;

    public PdfPageCache(IPdfPageInternal page, IPdfDocumentInternal document, PdfDictionary resources)
    {
        _page = page;
        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfPageCache>();
        ColorSpace = new PdfColorSpaceResolver(document, resources);
        _fontDictionary = resources.GetDictionary(PdfTokens.FontKey);
        _patternDictionary = resources.GetDictionary(PdfTokens.PatternKey);
        _extGStateDictionary = resources.GetDictionary(PdfTokens.ExtGStateKey);
        _xObjectDictionary = resources.GetDictionary(PdfTokens.XObjectKey);
        _shadingDictionary = resources.GetDictionary(PdfTokens.ShadingKey);
        _propertiesDictionary = resources.GetDictionary(PdfTokens.PropertiesKey);
    }

    /// <summary>
    /// Gets the resolver used to determine the color space for image processing operations.
    /// </summary>
    public PdfColorSpaceResolver ColorSpace { get; }

    /// <summary>
    /// Get (and cache) an XObject by resource name from the /XObject dictionary. Returns null if not found.
    /// Checks the document-level XObject cache by indirect reference before resolving the object.
    /// </summary>
    public PdfXObject? GetXObject(in PdfString xObjectName)
    {
        if (_xObjectsByName.TryGetValue(xObjectName, out PdfXObject? cachedXObject))
        {
            return cachedXObject;
        }

        if (_xObjectDictionary == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' requested but the page has no /XObject resources.", xObjectName);
            return null;
        }

        PdfReference? xObjectReference = _xObjectDictionary.GetReference(xObjectName);
        if (xObjectReference != null && _document.ObjectCache.XObjects.TryGetValue(xObjectReference.Value, out PdfXObject? documentCachedXObject))
        {
            _xObjectsByName[xObjectName] = documentCachedXObject;
            return documentCachedXObject;
        }

        PdfObject? pageObject = _xObjectDictionary.GetObject(xObjectName);
        if (pageObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' could not be resolved.", xObjectName);
            return null;
        }

        PdfXObject xObject = PdfXObject.FromObject(pageObject);
        _xObjectsByName[xObjectName] = xObject;

        if (pageObject.Reference.IsValid)
        {
            _document.ObjectCache.XObjects[pageObject.Reference] = xObject;
        }

        return xObject;
    }

    /// <summary>
    /// Get (and cache) a shading by resource name from the /Shading dictionary. Returns null if not found.
    /// Checks the document-level shading cache by indirect reference before resolving the object.
    /// </summary>
    public PdfShading? GetShading(in PdfString shadingName)
    {
        if (_shadingsByName.TryGetValue(shadingName, out PdfShading? cachedShading))
        {
            return cachedShading;
        }

        if (_shadingDictionary == null)
        {
            _logger.LogWarning("Shading '{ShadingName}' requested but the page has no /Shading resources.", shadingName);
            return null;
        }

        PdfShading? shading = ResolveShading(_shadingDictionary, shadingName);
        if (shading == null)
        {
            _logger.LogWarning("Shading '{ShadingName}' is not present in the page's /Shading resources.", shadingName);
            return null;
        }

        _shadingsByName[shadingName] = shading;

        return shading;
    }

    /// <summary>
    /// Get a shading from the /Shading entry of a shading pattern dictionary. Returns null if the
    /// pattern declares none.
    /// </summary>
    public PdfShading? GetShadingForPattern(PdfDictionary patternDictionary)
        => ResolveShading(patternDictionary, PdfTokens.ShadingKey);

    /// <summary>
    /// Get (and cache) the optional content membership a /Properties resource name refers to. Returns
    /// null if not found or if the entry describes no membership.
    /// Checks the document-level membership cache by indirect reference before resolving the object.
    /// </summary>
    public PdfOptionalContentMembership? GetOptionalContent(in PdfString propertiesName)
    {
        if (_optionalContentByName.TryGetValue(propertiesName, out PdfOptionalContentMembership? cachedMembership))
        {
            return cachedMembership;
        }

        if (_propertiesDictionary == null)
        {
            _logger.LogWarning("Properties '{PropertiesName}' requested but the page has no /Properties resources.", propertiesName);
            return null;
        }

        PdfReference? propertiesReference = _propertiesDictionary.GetReference(propertiesName);
        if (propertiesReference != null && _document.ObjectCache.OptionalContentMemberships.TryGetValue(propertiesReference.Value, out PdfOptionalContentMembership? documentCachedMembership))
        {
            _optionalContentByName[propertiesName] = documentCachedMembership;
            return documentCachedMembership;
        }

        PdfObject? propertiesObject = _propertiesDictionary.GetObject(propertiesName);
        if (propertiesObject == null)
        {
            _logger.LogWarning("Properties '{PropertiesName}' is not present in the page's /Properties resources.", propertiesName);
            return null;
        }

        PdfOptionalContentMembership? membership = PdfOptionalContentMembership.FromOptionalContentObject(propertiesObject);
        _optionalContentByName[propertiesName] = membership;

        if (propertiesObject.Reference.IsValid)
        {
            _document.ObjectCache.OptionalContentMemberships[propertiesObject.Reference] = membership;
        }

        return membership;
    }

    /// <summary>
    /// Get the dictionary a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public PdfDictionary? GetProperties(in PdfString propertiesName)
        => _propertiesDictionary?.GetDictionary(propertiesName);

    /// <summary>
    /// Get (and cache) a font by resource name. Returns null if not found or cannot be created.
    /// Checks the document-level font cache by indirect reference before resolving the object.
    /// </summary>
    public PdfFontBase? GetFont(in PdfString fontName)
    {
        if (_fontsByName.TryGetValue(fontName, out PdfFontBase? cachedFont))
        {
            return cachedFont;
        }

        if (_fontDictionary == null)
        {
            _logger.LogWarning("Font '{FontName}' requested but the page has no /Font resources.", fontName);
            return null;
        }

        PdfReference? fontReference = _fontDictionary.GetReference(fontName);
        if (fontReference != null && _document.ObjectCache.Fonts.TryGetValue(fontReference.Value, out PdfFontBase? documentCachedFont))
        {
            _fontsByName[fontName] = documentCachedFont;
            return documentCachedFont;
        }

        PdfObject? fontObject = _fontDictionary.GetObject(fontName);
        if (fontObject == null)
        {
            _logger.LogWarning("Font '{FontName}' is not present in the page's /Font resources.", fontName);
            return null;
        }

        PdfFontBase? font = GetFont(fontObject);
        if (font != null)
        {
            _fontsByName[fontName] = font;
        }

        return font;
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
    public PdfPattern? GetPattern(in PdfString patternName)
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

        PdfReference? patternReference = _patternDictionary.GetReference(patternName);
        if (patternReference != null && _document.ObjectCache.Patterns.TryGetValue(patternReference.Value, out PdfPattern? documentCachedPattern))
        {
            _patternsByName[patternName] = documentCachedPattern;
            return documentCachedPattern;
        }

        PdfObject? patternObject = _patternDictionary.GetObject(patternName);

        if (patternObject == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' is not present in the page's /Pattern resources.", patternName);
            return null;
        }

        PdfPattern? parsedPattern = PdfPatternParser.ParsePattern(patternObject, _page);

        if (parsedPattern == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' could not be parsed.", patternName);
        }
        else
        {
            _patternsByName[patternName] = parsedPattern;

            if (patternObject.Reference.IsValid && parsedPattern.IsPageIndependent)
            {
                _document.ObjectCache.Patterns[patternObject.Reference] = parsedPattern;
            }
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
            parameters = ResolveGraphicsStateParameters(_extGStateDictionary, graphicsStateName);
            if (parameters == null)
            {
                _logger.LogWarning("ExtGState '{GraphicsStateName}' is not present in the page's /ExtGState resources.", graphicsStateName);
                return;
            }

            _graphicsStateParametersByName[graphicsStateName] = parameters;
        }

        parameters.ApplyToGraphicsState(graphicsState);
        if (parameters.TransformMatrix.HasValue)
        {
            processor.Process(new ConcatMatrixCommand(parameters.TransformMatrix.Value));
        }
    }

    /// <summary>
    /// Resolves the ExtGState parameters a dictionary entry names.
    /// </summary>
    private PdfGraphicsStateParameters? ResolveGraphicsStateParameters(PdfDictionary dictionary, in PdfString key)
    {
        PdfReference? graphicsStateReference = dictionary.GetReference(key);
        if (graphicsStateReference != null && _document.ObjectCache.GraphicsStateParameters.TryGetValue(graphicsStateReference.Value, out PdfGraphicsStateParameters? documentCachedParameters))
        {
            return documentCachedParameters;
        }

        PdfDictionary? graphicsStateDictionary = dictionary.GetDictionary(key);
        if (graphicsStateDictionary == null)
        {
            return null;
        }

        PdfGraphicsStateParameters parameters = PdfGraphicsStateParser.ParseGraphicsStateParametersFromDictionary(graphicsStateDictionary, _page);

        if (graphicsStateReference != null && parameters.SoftMask == null)
        {
            _document.ObjectCache.GraphicsStateParameters[graphicsStateReference.Value] = parameters;
        }

        return parameters;
    }

    /// <summary>
    /// Resolves the shading a dictionary entry names.
    /// </summary>
    private PdfShading? ResolveShading(PdfDictionary dictionary, in PdfString key)
    {
        PdfReference? shadingReference = dictionary.GetReference(key);
        if (shadingReference != null && _document.ObjectCache.Shadings.TryGetValue(shadingReference.Value, out PdfShading? documentCachedShading))
        {
            return documentCachedShading;
        }

        PdfObject? shadingObject = dictionary.GetObject(key);
        if (shadingObject == null)
        {
            return null;
        }

        return PdfShading.GetShading(shadingObject);
    }
}
