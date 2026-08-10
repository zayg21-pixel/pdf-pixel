using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Instance color space resolver bound to a single <see cref="PdfPage"/> providing
/// resolution, caching (by indirect object reference and by resource name), and default device space substitution.
/// Replaces static <c>PdfColorSpaces</c>. All methods are non-static to simplify testability.
/// </summary>
internal sealed partial class ColorSpaceResolver
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfDictionary _resources;
    private readonly PdfDictionary? _colorSpaceDictionary;

    private readonly PdfColorSpaceConverter _defaultGray;
    private readonly PdfColorSpaceConverter _defaultRgb;
    private readonly PdfColorSpaceConverter _defaultCmyk;

    public ColorSpaceResolver(IPdfDocumentInternal document, PdfDictionary resources)
    {
        _document = document;
        _resources = resources;
        _colorSpaceDictionary = resources.GetDictionary(PdfTokens.ColorSpaceKey);

        _defaultGray = ResolveDefaultDeviceSpace(PdfTokens.DefaultGrayKey, 1) ?? DeviceGrayConverter.Instance;
        _defaultRgb = ResolveDefaultDeviceSpace(PdfTokens.DefaultRGBKey, 3) ?? DeviceRgbConverter.Instance;
        _defaultCmyk = ResolveDefaultDeviceSpace(PdfTokens.DefaultCMYKKey, 4) ?? DeviceCmykConverter.Instance;
    }

    /// <summary>
    /// Resolves the appropriate <see cref="PdfColorSpaceConverter"/> for the specified <paramref name="pdfObject"/>.
    /// </summary>
    /// <remarks>If <paramref name="pdfObject"/> has a valid reference and a matching converter is found in
    /// the cache, the cached converter is returned. Otherwise, the converter is resolved from the object's value and
    /// may be cached for future use.</remarks>
    /// <param name="pdfObject">The PDF object representing a color space definition. Must not be <c>null</c>.</param>
    /// <param name="defaultComponents">The default number of color components to assume if the color space type cannot be determined. Must be a
    /// positive integer. The default is 3.</param>
    /// <returns>A <see cref="PdfColorSpaceConverter"/> instance corresponding to the color space described by <paramref
    /// name="pdfObject"/>.</returns>
    public PdfColorSpaceConverter? ResolveByObject(PdfObject? pdfObject, int defaultComponents = 3)
    {
        if (pdfObject == null)
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        if (TryResolveFromCache(pdfObject, out PdfColorSpaceConverter? cached))
        {
            return cached;
        }

        PdfColorSpaceConverter? result = ResolveByValue(pdfObject.Value, defaultComponents);

        TryStoreByReference(pdfObject, result);

        return result;
    }

    /// <summary>
    /// Resolve a color space converter from a generic value which may be:
    ///1) A name (device or resource key)
    ///2) A parameter array (e.g. [/ICCBased obj])
    ///3) Null (default device fallback).
    /// Results are cached by indirect reference (document-level) and by resource name (page-level) when applicable.
    /// </summary>
    public PdfColorSpaceConverter? ResolveByValue(IPdfValue? value, int defaultComponents = 3)
    {
        if (value == null)
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        if (!TryGetColorSpaceName(value, out PdfString familyName))
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        return ResolveByNameAndValue(familyName, value);
    }

    /// <summary>
    /// Resolve a device converter given component count (1 = Gray,3 = RGB,4 = CMYK).
    /// </summary>
    public PdfColorSpaceConverter? ResolveDeviceConverter(int components)
    {
        switch (components)
        {
            case 1:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);

            case 3:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceRGB);

            case 4:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceCMYK);

            default:
                return null;
        }
    }

    /// <summary>
    /// Resolve a specific device color space considering Default* resource overrides and OutputIntent profile.
    /// Uses name cache only (no dedicated fields).
    /// </summary>
    public PdfColorSpaceConverter ResolveDeviceConverter(PdfColorSpaceType colorSpace)
    {
        switch (colorSpace)
        {
            case PdfColorSpaceType.DeviceGray:
            {
                return _defaultGray;
            }
            case PdfColorSpaceType.DeviceRGB:
            {
                return _defaultRgb;
            }
            case PdfColorSpaceType.DeviceCMYK:
            {
                return _defaultCmyk;
            }
        }

        return _defaultRgb;
    }

    #region Internal Helpers

    private PdfColorSpaceConverter? ResolveDefaultDeviceSpace(in PdfString defaultKey, int n)
    {
        IPdfValue? defaultVal = _resources.GetValue(defaultKey);
        if (defaultVal != null)
        {
            PdfColorSpaceConverter? conv = ResolveByValue(defaultVal, -1); // Recursive parse; do not override components.
            if (conv is IccBasedConverter icc && icc.N == n)
            {
                return icc;
            }

            if (conv != null && conv.Components == n)
            {
                return conv;
            }
        }

        if (_document.ObjectCache.OutputIntentProfileConverter != null && _document.ObjectCache.OutputIntentProfileConverter.N == n)
        {
            return _document.ObjectCache.OutputIntentProfileConverter;
        }

        return null;
    }

    private PdfColorSpaceConverter ResolveByNameAndValue(in PdfString name, IPdfValue originalValue)
    {
        if (name.IsEmpty)
        {
            return DeviceRgbConverter.Instance;
        }

        PdfColorSpaceType family = name.AsEnum<PdfColorSpaceType>();
        switch (family)
        {
            case PdfColorSpaceType.DeviceGray:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);

            case PdfColorSpaceType.DeviceRGB:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceRGB);

            case PdfColorSpaceType.DeviceCMYK:
                return ResolveDeviceConverter(PdfColorSpaceType.DeviceCMYK);

            case PdfColorSpaceType.Indexed:
            {
                PdfColorSpaceConverter? conv = CreateIndexedColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.ICCBased:
            {
                PdfColorSpaceConverter? conv = CreateIccColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.CalGray:
            {
                PdfColorSpaceConverter? conv = CreateCalGrayColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.CalRGB:
            {
                PdfColorSpaceConverter? conv = CreateCalRrgbColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Lab:
            {
                PdfColorSpaceConverter? conv = CreateLabColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Pattern:
            {
                PdfColorSpaceConverter conv = CreatePatternColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Separation:
            {
                PdfColorSpaceConverter? conv = CreateSeparationColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.DeviceN:
            {
                PdfColorSpaceConverter? conv = CreateDeviceNColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            default:
            {
                // Resource name lookup path.
                if (_colorSpaceDictionary != null)
                {
                    PdfObject? resourceValue = _colorSpaceDictionary.GetObject(name);

                    if (TryResolveFromCache(resourceValue, out PdfColorSpaceConverter? cached) && cached != null)
                    {
                        return cached;
                    }

                    PdfColorSpaceConverter? resolved = ResolveByValue(resourceValue?.Value);

                    if (resolved != null)
                    {
                        TryStoreByReference(resourceValue, resolved);
                        return resolved;
                    }
                }

                return DeviceRgbConverter.Instance;
            }
        }
    }

    private bool TryGetColorSpaceName(IPdfValue value, out PdfString name)
    {
        if (value == null)
        {
            name = default;
            return false;
        }

        PdfString? nameValue = value.AsName();
        if (nameValue != null)
        {
            name = nameValue.Value;
            return !name.IsEmpty;
        }

        if (value.Type == PdfValueType.Array)
        {
            PdfArray? arr = value.AsArray();
            if (arr?.Count > 0)
            {
                name = arr.GetName(0);
                return !name.IsEmpty;
            }
        }

        name = default;
        return false;
    }

    private bool TryResolveFromCache(PdfObject? pdfObject, out PdfColorSpaceConverter? converter)
    {
        if (pdfObject == null)
        {
            converter = null;
            return false;
        }

        if (pdfObject.Reference.IsValid && _document.ObjectCache.ColorSpaceConverters.TryGetValue(pdfObject.Reference, out PdfColorSpaceConverter? existing))
        {
            converter = existing;
            return true;
        }

        converter = null;
        return false;
    }

    private bool TryStoreByReference(PdfObject? pdfObject, PdfColorSpaceConverter? converter)
    {
        if (pdfObject == null)
        {
            return false;
        }

        if (pdfObject.Reference.IsValid)
        {
            _document.ObjectCache.ColorSpaceConverters[pdfObject.Reference] = converter;
            return true;
        }

        return false;
    }

    #endregion
}
