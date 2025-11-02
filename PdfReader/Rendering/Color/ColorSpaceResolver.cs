using System;
using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Instance color space resolver bound to a single <see cref="PdfPage"/> providing
    /// resolution, caching (by indirect object reference and by resource name), and default device space substitution.
    /// Replaces static <c>PdfColorSpaces</c>. All methods are non-static to simplify testability.
    /// </summary>
    internal sealed class ColorSpaceResolver
    {
        private readonly PdfPage _page;
        private readonly PdfDictionary _colorSpaceDictionary; // Cached once per page.
        private readonly Dictionary<PdfString, PdfColorSpaceConverter> _nameCache = new Dictionary<PdfString, PdfColorSpaceConverter>();

        public ColorSpaceResolver(PdfPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _colorSpaceDictionary = _page.ResourceDictionary.GetDictionary(PdfTokens.ColorSpaceKey);
        }

        /// <summary>
        /// Resolve a color space converter from a generic value which may be:
        ///1) A name (device or resource key)
        ///2) A parameter array (e.g. [/ICCBased obj])
        ///3) Null (default device fallback).
        /// Results are cached by indirect reference (document-level) and by resource name (page-level) when applicable.
        /// </summary>
        public PdfColorSpaceConverter ResolveByValue(IPdfValue value, int defaultComponents =3)
        {
            if (value == null)
            {
                return ResolveDeviceConverter(defaultComponents);
            }

            if (!TryGetColorSpaceName(value, out var familyName))
            {
                return ResolveDeviceConverter(defaultComponents);
            }

            // Name-level cache hit first (includes device names once resolved).
            if (_nameCache.TryGetValue(familyName, out var cachedByName))
            {
                return cachedByName;
            }

            bool hasRef = TryGetColorSpaceObject(value, out var referencedObject);
            if (hasRef && TryResolveFromCache(referencedObject, out var cached))
            {
                // Also store in name cache when name present.
                _nameCache[familyName] = cached;
                return cached;
            }

            var result = ResolveByNameAndValue(familyName, value);

            // Store in caches where appropriate.
            if (hasRef)
            {
                TryStoreByReference(referencedObject, result);
            }
            if (!familyName.IsEmpty && result != null)
            {
                _nameCache[familyName] = result;
            }

            return result;
        }

        /// <summary>
        /// Resolve a device converter given component count (1 = Gray,3 = RGB,4 = CMYK).
        /// </summary>
        public PdfColorSpaceConverter ResolveDeviceConverter(int components)
        {
            switch (components)
            {
                case 1:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceGray);
                case 3:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceRGB);
                case 4:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceCMYK);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Resolve a specific device color space considering Default* resource overrides and OutputIntent profile.
        /// Uses name cache only (no dedicated fields).
        /// </summary>
        public PdfColorSpaceConverter ResolveDeviceConverter(PdfColorSpace colorSpace)
        {
            var deviceName = colorSpace.AsPdfString();

            switch (colorSpace)
            {
                case PdfColorSpace.DeviceGray:
                {
                    if (_nameCache.TryGetValue(deviceName, out var gray))
                    {
                        return gray;
                    }
                    var resolved = ResolveDefaultDeviceSpace(PdfTokens.DefaultGrayKey,1) ?? DeviceGrayConverter.Instance;
                    _nameCache[deviceName] = resolved;
                    return resolved;
                }
                case PdfColorSpace.DeviceRGB:
                {
                    if (_nameCache.TryGetValue(deviceName, out var rgb))
                    {
                        return rgb;
                    }
                    var resolved = ResolveDefaultDeviceSpace(PdfTokens.DefaultRGBKey,3) ?? DeviceRgbConverter.Instance;
                    _nameCache[deviceName] = resolved;
                    return resolved;
                }
                case PdfColorSpace.DeviceCMYK:
                {
                    if (_nameCache.TryGetValue(deviceName, out var cmyk))
                    {
                        return cmyk;
                    }
                    var resolved = ResolveDefaultDeviceSpace(PdfTokens.DefaultCMYKKey,4) ?? DeviceCmykConverter.Instance;
                    _nameCache[deviceName] = resolved;
                    return resolved;
                }
            }
            return ResolveDeviceConverter(PdfColorSpace.DeviceRGB);
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

            if (_page.Document.OutputIntentProfile != null)
            {
                return new IccBasedConverter(n, null, _page.Document.OutputIntentProfile);
            }

            return null;
        }

        private PdfColorSpaceConverter ResolveByNameAndValue(PdfString name, IPdfValue originalValue)
        {
            if (name.IsEmpty)
            {
                return DeviceRgbConverter.Instance;
            }

            var family = name.AsEnum<PdfColorSpace>();
            switch (family)
            {
                case PdfColorSpace.DeviceGray:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceGray);
                case PdfColorSpace.DeviceRGB:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceRGB);
                case PdfColorSpace.DeviceCMYK:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceCMYK);
                case PdfColorSpace.Indexed:
                {
                    var conv = ColorSpaceFactory.CreateIndexedColorSpace(originalValue, _page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.ICCBased:
                {
                    var conv = ColorSpaceFactory.CreateIccColorSpace(originalValue, _page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.CalGray:
                {
                    var conv = ColorSpaceFactory.CreateCalGrayColorSpace(originalValue, _page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.CalRGB:
                {
                    var conv = ColorSpaceFactory.CreateCalRrgbColorSpace(originalValue, _page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Lab:
                {
                    var conv = ColorSpaceFactory.CreateLabColorSpace(originalValue, _page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Pattern:
                {
                    var conv = ColorSpaceFactory.CreatePatternColorSpace(originalValue, _page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Separation:
                {
                    var conv = ColorSpaceFactory.CreateSeparationColorSpace(originalValue, _page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.DeviceN:
                {
                    var conv = ColorSpaceFactory.CreateDeviceNColorSpace(originalValue, _page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                default:
                {
                    // Resource name lookup path.
                    if (_colorSpaceDictionary != null)
                    {
                        var resourceValue = _colorSpaceDictionary.GetValue(name);
                        if (resourceValue != null)
                        {
                            var resolved = ResolveByValue(resourceValue);
                            if (resolved != null)
                            {
                                _nameCache[name] = resolved;
                                return resolved;
                            }
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
                if (arr != null && arr.Count >0)
                {
                    name = arr.GetName(0);
                    return !name.IsEmpty;
                }
            }
            name = default;
            return false;
        }

        private bool TryGetColorSpaceObject(IPdfValue value, out PdfObject pdfObject)
        {
            if (value != null && value.Type == PdfValueType.Array)
            {
                var arr = value.AsArray();
                if (arr != null && arr.Count ==2)
                {
                    pdfObject = arr.GetPageObject(1);
                    return pdfObject?.Reference.IsValid == true;
                }
            }
            pdfObject = null;
            return false;
        }

        private bool TryResolveFromCache(PdfObject pdfObject, out PdfColorSpaceConverter converter)
        {
            if (pdfObject.Reference.IsValid && _page.Document.ColorSpaceConverters.TryGetValue(pdfObject.Reference, out var existing))
            {
                converter = existing;
                return true;
            }
            converter = null;
            return false;
        }

        private bool TryStoreByReference(PdfObject pdfObject, PdfColorSpaceConverter converter)
        {
            if (pdfObject.Reference.IsValid)
            {
                _page.Document.ColorSpaceConverters[pdfObject.Reference] = converter;
                return true;
            }
            return false;
        }

        #endregion
    }
}
