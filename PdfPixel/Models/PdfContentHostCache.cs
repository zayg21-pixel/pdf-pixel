using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Model;
using PdfPixel.Files;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Per-host name-based resource cache to avoid repeated dictionary lookups and conversions.
/// Caches fonts, patterns, and color spaces by their resource name (e.g. /F1, /P1, /CS1).
/// Underlying PdfDocument still caches referenced resources by indirect object reference.
/// </summary>
internal sealed class PdfContentHostCache
{
    private readonly IPdfDocumentInternal _document;
    private readonly ILogger<PdfContentHostCache> _logger;
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

    public PdfContentHostCache(IPdfDocumentInternal document, PdfDictionary resources)
    {
        _document = document;
        _logger = document.LoggerFactory.CreateLogger<PdfContentHostCache>();
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

        PdfXObject? xObject = _document.ObjectCache.GetOrCreate(_xObjectDictionary, xObjectName, PdfXObject.FromObject);
        if (xObject == null)
        {
            _logger.LogWarning("XObject '{XObjectName}' could not be resolved.", xObjectName);
            return null;
        }

        _xObjectsByName[xObjectName] = xObject;

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

        PdfShading? shading = PdfShading.GetShading(_shadingDictionary, shadingName);
        if (shading == null)
        {
            _logger.LogWarning("Shading '{ShadingName}' is not present in the page's /Shading resources.", shadingName);
            return null;
        }

        _shadingsByName[shadingName] = shading;

        return shading;
    }

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

        if (!_propertiesDictionary.HasKey(propertiesName))
        {
            _logger.LogWarning("Properties '{PropertiesName}' is not present in the page's /Properties resources.", propertiesName);
            return null;
        }

        PdfOptionalContentMembership? membership = _document.ObjectCache.GetOrCreate(_propertiesDictionary, propertiesName, PdfOptionalContentMembership.FromObject);
        _optionalContentByName[propertiesName] = membership;

        return membership;
    }

    /// <summary>
    /// Get the dictionary a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public PdfDictionary? GetProperties(in PdfString propertiesName)
        => _propertiesDictionary?.GetDictionary(propertiesName);

    /// <summary>
    /// Get the associated files a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public List<PdfFileSpecification>? GetAssociatedFiles(in PdfString propertiesName)
        => PdfFileSpecification.GetFileSpecifications(_propertiesDictionary?.GetArray(propertiesName));

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

        PdfFontBase? font = _document.ObjectCache.GetOrCreate(_fontDictionary, fontName, PdfFontFactory.CreateFont);
        if (font == null)
        {
            _logger.LogWarning("Font '{FontName}' could not be resolved from the page's /Font resources.", fontName);
            return null;
        }

        _fontsByName[fontName] = font;

        return font;
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

        // we're capturing host here for external graphics state of pattern, even though it's now "page level" content, in practice it's just to have consistent API
        // we it's doubtful that pattern would need something from host besides render time
        PdfPattern? pattern = _document.ObjectCache.GetOrCreate(_patternDictionary, patternName, PdfPatternParser.ParsePattern);
        if (pattern == null)
        {
            _logger.LogWarning("Pattern '{PatternName}' could not be resolved from the page's /Pattern resources.", patternName);
            return null;
        }

        _patternsByName[patternName] = pattern;

        return pattern;
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
            parameters = PdfGraphicsStateParser.GetGraphicsStateParameters(_extGStateDictionary, graphicsStateName);
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
}
