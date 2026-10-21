using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
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
/// Resources a content stream resolves its names against: a resource dictionary and, for pages, the
/// resources inherited from the page tree. Each name is looked up in the own dictionary first and then
/// in the inherited resources.
/// </summary>
internal sealed class PdfResourceNode
{
    private readonly ILogger<PdfResourceNode> _logger;
    private readonly PdfResourceDictionary _resources;
    private readonly PdfResourceNode? _parent;
    private readonly PdfColorSpaceResolver _colorSpaceResolver;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="document">Owning document.</param>
    /// <param name="resources">Own resource dictionary.</param>
    /// <param name="parent">Inherited resources, or null when nothing is inherited.</param>
    public PdfResourceNode(IPdfDocumentInternal document, PdfResourceDictionary resources, PdfResourceNode? parent)
    {
        _logger = document.LoggerFactory.CreateLogger<PdfResourceNode>();
        _resources = resources;
        _parent = parent;
        _colorSpaceResolver = document.ColorSpaceResolver;
        DeviceColorMapping = new PdfDeviceColorMapping(
            ResolveDeviceColorSpace(document, PdfTokens.DefaultGrayKey, 1),
            ResolveDeviceColorSpace(document, PdfTokens.DefaultRGBKey, 3),
            ResolveDeviceColorSpace(document, PdfTokens.DefaultCMYKKey, 4));
    }

    /// <summary>
    /// Color spaces device color values selected with these resources are interpreted in.
    /// </summary>
    public PdfDeviceColorMapping DeviceColorMapping { get; }

    /// <summary>
    /// Creates resources holding no resource dictionary entries.
    /// </summary>
    /// <param name="document">Owning document.</param>
    public static PdfResourceNode CreateEmpty(IPdfDocumentInternal document)
    {
        PdfResourceDictionary emptyResources = new(new PdfDictionary(document));
        return new PdfResourceNode(document, emptyResources, null);
    }

    /// <summary>
    /// Get an XObject by resource name. Returns null if not found.
    /// </summary>
    public PdfXObject? GetXObject(in PdfString xObjectName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfXObject? xObject = node._resources.GetXObject(xObjectName);
            if (xObject != null)
            {
                return xObject;
            }
        }

        _logger.LogWarning("XObject '{XObjectName}' could not be resolved from the /XObject resources.", xObjectName);
        return null;
    }

    /// <summary>
    /// Get a shading by resource name. Returns null if not found.
    /// </summary>
    public PdfShading? GetShading(in PdfString shadingName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfShading? shading = node._resources.GetShading(shadingName);
            if (shading != null)
            {
                return shading;
            }
        }

        _logger.LogWarning("Shading '{ShadingName}' could not be resolved from the /Shading resources.", shadingName);
        return null;
    }

    /// <summary>
    /// Get the optional content membership a /Properties resource name refers to. Returns
    /// null if not found or if the entry describes no membership.
    /// </summary>
    public PdfOptionalContentMembership? GetOptionalContent(in PdfString propertiesName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfOptionalContentMembership? membership = node._resources.GetOptionalContent(propertiesName);
            if (membership != null)
            {
                return membership;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the dictionary a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public PdfDictionary? GetProperties(in PdfString propertiesName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfDictionary? properties = node._resources.GetProperties(propertiesName);
            if (properties != null)
            {
                return properties;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the associated files a /Properties resource name refers to. Returns null if not found.
    /// </summary>
    public List<PdfFileSpecification>? GetAssociatedFiles(in PdfString propertiesName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            List<PdfFileSpecification>? associatedFiles = node._resources.GetAssociatedFiles(propertiesName);
            if (associatedFiles != null)
            {
                return associatedFiles;
            }
        }

        return null;
    }

    /// <summary>
    /// Get a font by resource name. Returns null if not found or cannot be created.
    /// </summary>
    public PdfFont? GetFont(in PdfString fontName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfFont? font = node._resources.GetFont(fontName);
            if (font != null)
            {
                return font;
            }
        }

        _logger.LogWarning("Font '{FontName}' could not be resolved from the /Font resources.", fontName);
        return null;
    }

    /// <summary>
    /// Get a pattern by resource name. Returns null if not found or unsupported.
    /// </summary>
    public PdfPattern? GetPattern(in PdfString patternName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfPattern? pattern = node._resources.GetPattern(patternName);
            if (pattern != null)
            {
                return pattern;
            }
        }

        _logger.LogWarning("Pattern '{PatternName}' could not be resolved from the /Pattern resources.", patternName);
        return null;
    }

    /// <summary>
    /// Apply graphics state parameters from an ExtGState entry identified by name.
    /// Applies them to the graphicsState and concatenates any transform matrix.
    /// </summary>
    /// <param name="graphicsStateName">Name of the ExtGState resource (/GS)</param>
    /// <param name="processor">Command processor for matrix concatenation</param>
    /// <param name="graphicsState">Graphics state to update</param>
    public void ApplyGraphicsStateParameters(in PdfString graphicsStateName, IPdfCommandProcessor processor, PdfGraphicsState graphicsState)
    {
        if (processor == null || graphicsState == null)
        {
            return;
        }

        PdfGraphicsStateParameters? parameters = null;
        for (var node = this; node != null && parameters == null; node = node._parent)
        {
            parameters = node._resources.GetGraphicsStateParameters(graphicsStateName);
        }

        if (parameters == null)
        {
            _logger.LogWarning("ExtGState '{GraphicsStateName}' is not present in the /ExtGState resources.", graphicsStateName);
            return;
        }

        parameters.ApplyToGraphicsState(graphicsState);
        if (parameters.TransformMatrix.HasValue)
        {
            processor.Process(new ConcatMatrixCommand(parameters.TransformMatrix.Value));
        }
    }

    /// <summary>
    /// Resolves a color space as written. A /ColorSpace resource name is looked up in these resources; anything
    /// else is resolved by the document.
    /// </summary>
    /// <param name="colorSpace">The color space as written.</param>
    /// <param name="defaultComponents">Component count to assume when no color space was written.</param>
    public PdfColorSpaceConverter? ResolveColorSpace(in PdfColorSpaceReference colorSpace, int defaultComponents = 3)
    {
        if (colorSpace.Reference == null && colorSpace.Value != null)
        {
            return ResolveColorSpace(colorSpace.Value, defaultComponents);
        }

        return _colorSpaceResolver.Resolve(colorSpace, defaultComponents);
    }

    /// <summary>
    /// Resolves a color space operand. A /ColorSpace resource name is looked up in these resources; DeviceGray,
    /// DeviceRGB, DeviceCMYK and Pattern always name the color space itself, and arrays are resolved by the document.
    /// </summary>
    /// <param name="value">The color space operand.</param>
    /// <param name="defaultComponents">Component count to assume when no color space was written.</param>
    public PdfColorSpaceConverter? ResolveColorSpace(PdfValue? value, int defaultComponents = 3)
    {
        PdfString? name = value?.AsName();
        if (name != null && !IsColorSpaceFamilyName(name.Value))
        {
            PdfColorSpaceReference resource = GetColorSpace(name.Value);
            if (resource.IsPresent)
            {
                return _colorSpaceResolver.Resolve(resource, defaultComponents);
            }
        }

        return _colorSpaceResolver.ResolveByValue(value, defaultComponents);
    }

    /// <summary>
    /// Get the color space a /ColorSpace resource name refers to, as written. Not present if not found.
    /// </summary>
    public PdfColorSpaceReference GetColorSpace(in PdfString colorSpaceName)
    {
        for (var node = this; node != null; node = node._parent)
        {
            PdfColorSpaceReference colorSpace = node._resources.GetColorSpace(colorSpaceName);
            if (colorSpace.IsPresent)
            {
                return colorSpace;
            }
        }

        return default;
    }

    private PdfColorSpaceConverter? ResolveDeviceColorSpace(IPdfDocumentInternal document, in PdfString defaultKey, int componentCount)
    {
        PdfColorSpaceReference defaultColorSpace = GetColorSpace(defaultKey);
        if (defaultColorSpace.IsPresent)
        {
            PdfColorSpaceConverter? converter = ResolveColorSpace(defaultColorSpace, -1);
            if (converter is PdfIccColorSpaceConverter iccConverter && iccConverter.N == componentCount)
            {
                return iccConverter;
            }

            if (converter != null && converter.Components == componentCount)
            {
                return converter;
            }
        }

        PdfIccColorSpaceConverter? outputIntentConverter = document.OutputIntentConverter;
        if (outputIntentConverter != null && outputIntentConverter.N == componentCount)
        {
            return outputIntentConverter;
        }

        return null;
    }

    private static bool IsColorSpaceFamilyName(in PdfString name)
    {
        PdfColorSpaceType family = name.AsEnum<PdfColorSpaceType>();

        return family == PdfColorSpaceType.DeviceGray
            || family == PdfColorSpaceType.DeviceRGB
            || family == PdfColorSpaceType.DeviceCMYK
            || family == PdfColorSpaceType.Pattern;
    }
}
