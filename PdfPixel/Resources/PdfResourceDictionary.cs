using PdfPixel.Color.ColorSpace;
using PdfPixel.Files;
using PdfPixel.Fonts;
using PdfPixel.Objects;
using PdfPixel.OptionalContent;
using PdfPixel.Parsing;
using PdfPixel.Pattern;
using PdfPixel.Rendering;
using PdfPixel.Shading;
using System.Collections.Generic;

namespace PdfPixel.Resources;

/// <summary>
/// A single /Resources dictionary with name-based lookups of the resources it declares.
/// Caches fonts, patterns, XObjects and other resources by their resource name (e.g. /F1, /P1, /Im1).
/// Underlying PdfDocument still caches referenced resources by indirect object reference.
/// Lookups return null for names the dictionary does not declare; <see cref="PdfResourceNode"/> reports them.
/// </summary>
internal sealed class PdfResourceDictionary
{
    private readonly IPdfDocumentInternal _document;
    private readonly Dictionary<PdfString, PdfFont?> _fontsByName = [];
    private readonly Dictionary<PdfString, PdfPattern?> _patternsByName = [];
    private readonly Dictionary<PdfString, PdfGraphicsStateParameters?> _graphicsStateParametersByName = [];
    private readonly Dictionary<PdfString, PdfXObject?> _xObjectsByName = [];
    private readonly Dictionary<PdfString, PdfShading?> _shadingsByName = [];
    private readonly Dictionary<PdfString, PdfOptionalContentMembership?> _optionalContentByName = [];
    private readonly Dictionary<PdfString, PdfDictionary?> _propertiesByName = [];
    private readonly Dictionary<PdfString, List<PdfFileSpecification>?> _associatedFilesByName = [];
    private readonly PdfDictionary? _fontDictionary;
    private readonly PdfDictionary? _patternDictionary;
    private readonly PdfDictionary? _extGStateDictionary;
    private readonly PdfDictionary? _xObjectDictionary;
    private readonly PdfDictionary? _shadingDictionary;
    private readonly PdfDictionary? _propertiesDictionary;
    private readonly PdfDictionary? _colorSpaceDictionary;

    public PdfResourceDictionary(PdfDictionary dictionary)
    {
        _document = dictionary.Document;
        _colorSpaceDictionary = dictionary.GetDictionary(PdfTokens.ColorSpaceKey);
        _fontDictionary = dictionary.GetDictionary(PdfTokens.FontKey);
        _patternDictionary = dictionary.GetDictionary(PdfTokens.PatternKey);
        _extGStateDictionary = dictionary.GetDictionary(PdfTokens.ExtGStateKey);
        _xObjectDictionary = dictionary.GetDictionary(PdfTokens.XObjectKey);
        _shadingDictionary = dictionary.GetDictionary(PdfTokens.ShadingKey);
        _propertiesDictionary = dictionary.GetDictionary(PdfTokens.PropertiesKey);
    }

    /// <summary>
    /// Creates the resource dictionary for a /Resources object.
    /// </summary>
    /// <param name="resourcesObject">The /Resources object.</param>
    /// <returns>The resource dictionary, or null when the object is not a dictionary.</returns>
    public static PdfResourceDictionary? FromObject(PdfObject resourcesObject)
    {
        PdfDictionary? dictionary = resourcesObject.Value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfResourceDictionary(dictionary);
    }

    /// <summary>
    /// Gets the resource dictionary of the /Resources entry <paramref name="key"/> of <paramref name="owner"/>,
    /// shared document-wide when the entry is indirect.
    /// </summary>
    /// <param name="owner">Dictionary holding the entry.</param>
    /// <param name="key">Key of the entry.</param>
    /// <returns>The resource dictionary, or null when the entry is absent.</returns>
    public static PdfResourceDictionary? GetResourceDictionary(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    /// <summary>
    /// Get the color space a /ColorSpace resource name (including /DefaultGray, /DefaultRGB and /DefaultCMYK) refers to,
    /// as written. Not present if not found.
    /// </summary>
    public PdfColorSpaceReference GetColorSpace(in PdfString colorSpaceName)
        => PdfColorSpaceReference.FromDictionary(_colorSpaceDictionary, colorSpaceName);

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
            return null;
        }

        PdfXObject? xObject = PdfXObject.GetXObject(_xObjectDictionary, xObjectName);
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
            return null;
        }

        PdfShading? shading = PdfShading.GetShading(_shadingDictionary, shadingName);
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

        PdfOptionalContentMembership? membership = _document.ObjectCache.GetOrCreate(_propertiesDictionary, propertiesName, PdfOptionalContentMembership.FromObject);
        _optionalContentByName[propertiesName] = membership;

        return membership;
    }

    /// <summary>
    /// Get the dictionary a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public PdfDictionary? GetProperties(in PdfString propertiesName)
    {
        if (_propertiesByName.TryGetValue(propertiesName, out PdfDictionary? cachedProperties))
        {
            return cachedProperties;
        }

        PdfDictionary? properties = _propertiesDictionary?.GetDictionary(propertiesName);
        _propertiesByName[propertiesName] = properties;

        return properties;
    }

    /// <summary>
    /// Get the associated files a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public List<PdfFileSpecification>? GetAssociatedFiles(in PdfString propertiesName)
    {
        if (_associatedFilesByName.TryGetValue(propertiesName, out List<PdfFileSpecification>? cachedAssociatedFiles))
        {
            return cachedAssociatedFiles;
        }

        List<PdfFileSpecification>? associatedFiles = PdfFileSpecification.GetFileSpecifications(_propertiesDictionary?.GetArray(propertiesName));
        _associatedFilesByName[propertiesName] = associatedFiles;

        return associatedFiles;
    }

    /// <summary>
    /// Get (and cache) a font by resource name. Returns null if not found or cannot be created.
    /// Checks the document-level font cache by indirect reference before resolving the object.
    /// </summary>
    public PdfFont? GetFont(in PdfString fontName)
    {
        if (_fontsByName.TryGetValue(fontName, out PdfFont? cachedFont))
        {
            return cachedFont;
        }

        if (_fontDictionary == null)
        {
            return null;
        }

        PdfFont? font = PdfFont.GetFont(_fontDictionary, fontName);
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

        // we're capturing host here for external graphics state of pattern, even though it's now "page level" content, in practice it's just to have consistent API
        // we it's doubtful that pattern would need something from host besides render time
        PdfPattern? pattern = _document.ObjectCache.GetOrCreate(_patternDictionary, patternName, PdfPatternParser.ParsePattern);
        _patternsByName[patternName] = pattern;

        return pattern;
    }

    /// <summary>
    /// Get (and cache) the graphics state parameters of an ExtGState entry identified by name. Returns null if not found.
    /// </summary>
    /// <param name="graphicsStateName">Name of the ExtGState resource (/GS)</param>
    public PdfGraphicsStateParameters? GetGraphicsStateParameters(in PdfString graphicsStateName)
    {
        if (_graphicsStateParametersByName.TryGetValue(graphicsStateName, out PdfGraphicsStateParameters? cachedParameters))
        {
            return cachedParameters;
        }

        if (_extGStateDictionary == null)
        {
            return null;
        }

        PdfGraphicsStateParameters? parameters = PdfGraphicsStateParameters.GetGraphicsStateParameters(_extGStateDictionary, graphicsStateName);
        _graphicsStateParametersByName[graphicsStateName] = parameters;

        return parameters;
    }
}
