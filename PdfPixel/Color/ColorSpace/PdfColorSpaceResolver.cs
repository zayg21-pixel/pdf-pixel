using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Resources;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Document color space resolver providing resolution and caching by indirect object reference. It knows no
/// resources: names of /ColorSpace resources are looked up by <see cref="PdfResourceNode"/>. Device color spaces
/// resolve as is; how their values are interpreted is decided by <see cref="PdfDeviceColorMapping"/> at conversion.
/// </summary>
internal sealed partial class PdfColorSpaceResolver
{
    private readonly IPdfDocumentInternal _document;

    public PdfColorSpaceResolver(IPdfDocumentInternal document) => _document = document;

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

        if (pdfObject.Reference != null)
        {
            return ResolveByReference(pdfObject.Reference.Value, defaultComponents);
        }

        return ResolveByValue(pdfObject.Value, defaultComponents);
    }

    /// <summary>
    /// Resolves the converter for a color space as it was written. An indirect color space is
    /// answered from the document cache by its reference, so the object it names is resolved only the
    /// first time any user of it asks.
    /// </summary>
    /// <param name="colorSpaceReference">The color space as written.</param>
    /// <param name="defaultComponents">Component count to assume when no color space was written.</param>
    public PdfColorSpaceConverter? Resolve(in PdfColorSpaceReference colorSpaceReference, int defaultComponents = 3)
    {
        if (colorSpaceReference.Reference != null)
        {
            return ResolveByReference(colorSpaceReference.Reference.Value, defaultComponents);
        }

        if (colorSpaceReference.Value != null)
        {
            return ResolveByValue(colorSpaceReference.Value, defaultComponents);
        }

        return ResolveDeviceConverter(defaultComponents);
    }

    /// <summary>
    /// Resolve a color space converter from a generic value which may be:
    ///1) A color space family name
    ///2) A parameter array (e.g. [/ICCBased obj])
    ///3) Null (default device fallback).
    /// Results are cached by indirect reference (document-level) when applicable.
    /// </summary>
    public PdfColorSpaceConverter? ResolveByValue(PdfValue? value, int defaultComponents = 3)
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
    public static PdfColorSpaceConverter? ResolveDeviceConverter(int components)
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
    /// Resolve a specific device color space.
    /// </summary>
    public static PdfColorSpaceConverter ResolveDeviceConverter(PdfColorSpaceType colorSpace)
    {
        switch (colorSpace)
        {
            case PdfColorSpaceType.DeviceGray:
            {
                return PdfDeviceGrayColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.DeviceRGB:
            {
                return PdfDeviceRgbColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.DeviceCMYK:
            {
                return PdfDeviceCmykColorSpaceConverter.Instance;
            }
        }

        return PdfDeviceRgbColorSpaceConverter.Instance;
    }

    private PdfColorSpaceConverter ResolveByNameAndValue(in PdfString name, PdfValue originalValue)
    {
        if (name.IsEmpty)
        {
            return PdfDeviceRgbColorSpaceConverter.Instance;
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
                return conv ?? PdfDeviceGrayColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.ICCBased:
            {
                PdfColorSpaceConverter? conv = CreateIccColorSpace(originalValue);
                return conv ?? PdfDeviceRgbColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.CalGray:
            {
                PdfColorSpaceConverter? conv = CreateCalGrayColorSpace(originalValue);
                return conv ?? PdfDeviceGrayColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.CalRGB:
            {
                PdfColorSpaceConverter? conv = CreateCalRrgbColorSpace(originalValue);
                return conv ?? PdfDeviceRgbColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.Lab:
            {
                PdfColorSpaceConverter? conv = CreateLabColorSpace(originalValue);
                return conv ?? PdfDeviceRgbColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.Pattern:
            {
                PdfColorSpaceConverter conv = CreatePatternColorSpace(originalValue);
                return conv ?? PdfDeviceRgbColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.Separation:
            {
                PdfColorSpaceConverter? conv = CreateSeparationColorSpace(originalValue);
                return conv ?? PdfDeviceGrayColorSpaceConverter.Instance;
            }
            case PdfColorSpaceType.DeviceN:
            {
                PdfColorSpaceConverter? conv = CreateDeviceNColorSpace(originalValue);
                return conv ?? PdfDeviceRgbColorSpaceConverter.Instance;
            }
            default:
            {
                return PdfDeviceRgbColorSpaceConverter.Instance;
            }
        }
    }

    private bool TryGetColorSpaceName(PdfValue value, out PdfString name)
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
            return true;
        }

        if (value is PdfArrayValue)
        {
            PdfArray? arr = value.AsArray();
            PdfString? firstName = (arr?.Count > 0) ? arr.GetName(0) : null;

            if (firstName != null)
            {
                name = firstName.Value;
                return true;
            }
        }

        name = default;
        return false;
    }

    private PdfColorSpaceConverter? ResolveByReference(in PdfReference reference, int defaultComponents)
    {
        PdfColorSpaceConverter? converter = _document.ObjectCache.GetOrCreate(reference, colorSpaceObject => ResolveByValue(colorSpaceObject.Value, -1));

        return converter ?? ResolveDeviceConverter(defaultComponents);
    }
}
