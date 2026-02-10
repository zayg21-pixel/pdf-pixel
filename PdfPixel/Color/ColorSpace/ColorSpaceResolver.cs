using System;
using System.Collections.Generic;
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
    private readonly PdfPage _page;
    private readonly PdfDictionary _colorSpaceDictionary; // Cached once per page.

    public ColorSpaceResolver(PdfPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _colorSpaceDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.ColorSpaceKey);
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
    public PdfColorSpaceConverter ResolveByObject(PdfObject pdfObject, int defaultComponents = 3)
    {
        if (pdfObject == null)
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        if (TryResolveFromCache(pdfObject, out var cached))
        {
            return cached;
        }

        var result = ResolveByValue(pdfObject.Value, defaultComponents);

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
    public PdfColorSpaceConverter ResolveByValue(IPdfValue value, int defaultComponents = 3)
    {
        if (value == null)
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        if (!TryGetColorSpaceName(value, out var familyName))
        {
            return ResolveDeviceConverter(defaultComponents);
        }

        return ResolveByNameAndValue(familyName, value);
    }

    /// <summary>
    /// Resolve a device converter given component count (1 = Gray,3 = RGB,4 = CMYK).
    /// </summary>
    public PdfColorSpaceConverter ResolveDeviceConverter(int components)
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
                return ResolveDefaultDeviceSpace(PdfTokens.DefaultGrayKey, 1) ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.DeviceRGB:
            {
                return ResolveDefaultDeviceSpace(PdfTokens.DefaultRGBKey, 3) ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.DeviceCMYK:
            {
                return ResolveDefaultDeviceSpace(PdfTokens.DefaultCMYKKey, 4) ?? DeviceCmykConverter.Instance;
            }
        }
        return ResolveDeviceConverter(PdfColorSpaceType.DeviceRGB);
    }

    #region Internal Helpers

    private PdfColorSpaceConverter ResolveDefaultDeviceSpace(PdfString defaultKey, int n)
    {
        var defaultVal = _page.ResourceDictionary.GetValue(defaultKey);
        if (defaultVal != null)
        {
            var conv = ResolveByValue(defaultVal, -1); // Recursive parse; do not override components.
            if (conv is IccBasedConverter icc && icc.N == n)
            {
                return icc;
            }
            if (conv != null && conv.Components == n)
            {
                return conv;
            }
        }

        if (_page.Document.ObjectCache.OutputIntentProfileConverter != null && _page.Document.ObjectCache.OutputIntentProfileConverter.N == n)
        {
            return _page.Document.ObjectCache.OutputIntentProfileConverter;
        }

        return null;
    }

    private PdfColorSpaceConverter ResolveByNameAndValue(PdfString name, IPdfValue originalValue)
    {
        if (name.IsEmpty)
        {
            return DeviceRgbConverter.Instance;
        }

        var family = name.AsEnum<PdfColorSpaceType>();
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
                var conv = CreateIndexedColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.ICCBased:
            {
                var conv = CreateIccColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.CalGray:
            {
                var conv = CreateCalGrayColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.CalRGB:
            {
                var conv = CreateCalRrgbColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Lab:
            {
                var conv = CreateLabColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Pattern:
            {
                var conv = CreatePatternColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            case PdfColorSpaceType.Separation:
            {
                var conv = CreateSeparationColorSpace(originalValue);
                return conv ?? DeviceGrayConverter.Instance;
            }
            case PdfColorSpaceType.DeviceN:
            {
                var conv = CreateDeviceNColorSpace(originalValue);
                return conv ?? DeviceRgbConverter.Instance;
            }
            default:
            {
                // Resource name lookup path.
                if (_colorSpaceDictionary != null)
                {
                    var resourceValue = _colorSpaceDictionary.GetObject(name);

                    if (TryResolveFromCache(resourceValue, out var cached))
                    {
                        return cached;
                    }

                    var resolved = ResolveByValue(resourceValue?.Value);

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
        if (value.Type == PdfValueType.Name)
        {
            name = value.AsName();
            return !name.IsEmpty;
        }
        if (value.Type == PdfValueType.Array)
        {
            var arr = value.AsArray();
            if (arr != null && arr.Count > 0)
            {
                name = arr.GetName(0);
                return !name.IsEmpty;
            }
        }
        name = default;
        return false;
    }

    private bool TryResolveFromCache(PdfObject pdfObject, out PdfColorSpaceConverter converter)
    {
        if (pdfObject == null)
        {
            converter = null;
            return false;
        }

        if (pdfObject.Reference.IsValid && _page.Document.ObjectCache.ColorSpaceConverters.TryGetValue(pdfObject.Reference, out var existing))
        {
            converter = existing;
            return true;
        }
        converter = null;
        return false;
    }

    private bool TryStoreByReference(PdfObject pdfObject, PdfColorSpaceConverter converter)
    {
        if (pdfObject == null)
        {
            return false;
        }

        if (pdfObject.Reference.IsValid)
        {
            _page.Document.ObjectCache.ColorSpaceConverters[pdfObject.Reference] = converter;
            return true;
        }
        return false;
    }

    #endregion
}
