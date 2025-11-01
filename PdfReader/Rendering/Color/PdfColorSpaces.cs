using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Rendering.Color
{
    public static class PdfColorSpaces
    {
        public static PdfColorSpaceConverter ResolveByValue(IPdfValue value, PdfPage page, int defaultComponents = 3)
        {
            if (value == null)
            {
                return ResolveDeviceConverter(defaultComponents, page);
            }

            if (!TryGetColorSpaceName(value, out var name))
            {
                return ResolveDeviceConverter(defaultComponents, page);
            }

            bool hasReference = TryGetColorSpaceObject(value, out var pdfObject);

            if (hasReference && TryResolveFromCache(pdfObject, out var cached))
            {
                return cached;
            }

            var result = ResolveByNameAndValue(name, value, page);

            if (hasReference)
            {
                TryStoreByReference(pdfObject, result);
            }

            return result;
        }

        public static PdfColorSpaceConverter ResolveDeviceConverter(int components, PdfPage page)
        {
            switch (components)
            {
                case 1:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceGray, page);
                case 3:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceRGB, page);
                case 4:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceCMYK, page);
                default:
                    return null;
            }
        }

        public static PdfColorSpaceConverter ResolveDeviceConverter(PdfColorSpace colorSpace, PdfPage page)
        {
            switch (colorSpace)
            {
                case PdfColorSpace.DeviceGray:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultGrayKey, 1) ?? DeviceGrayConverter.Instance;
                case PdfColorSpace.DeviceRGB:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultRGBKey, 3) ?? DeviceRgbConverter.Instance;
                case PdfColorSpace.DeviceCMYK:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultCMYKKey, 4) ?? DeviceCmykConverter.Instance;
            }

            return DeviceRgbConverter.Instance;
        }

        private static PdfColorSpaceConverter ResolveDefaultDeviceSpace(PdfPage page, PdfString defaultKey, int n)
        {
            var resources = page?.ResourceDictionary;
            var defaultVal = resources?.GetValue(defaultKey);
            if (defaultVal != null)
            {
                // Default.* can be an ICCBased space or other color space; reuse existing parser
                var conv = ResolveByValue(defaultVal, page, -1);
                if (conv is IccBasedConverter iccConv && iccConv.N == n)
                {
                    return iccConv; // Already ICC
                }

                // If it resolved to a non-ICC converter but N matches, wrap via ICC if underlying was ICCBased
                if (conv != null && conv.Components == n)
                {
                    return conv;
                }
            }

            // Fallback to first /DestOutputProfile if present (OutputIntent) for RGB/CMYK/Gray
            var profileBytes = TryGetFirstOutputIntentProfile(page.Document);
            if (profileBytes != null && profileBytes.Length > 0)
            {
                return new IccBasedConverter(n, null, profileBytes);
            }

            return null;
        }

        private static byte[] TryGetFirstOutputIntentProfile(PdfDocument document)
        {
            // TODO: find real example of this
            var rootObject = document.RootObject;
            if (rootObject == null)
            {
                return null;
            }

            var catalog = rootObject.Dictionary;
            var intents = catalog?.GetPageObjects(PdfTokens.OutputIntentsKey);
            if (intents == null)
            {
                return null;
            }

            foreach (var oi in intents)
            {
                var dict = oi?.Dictionary;
                if (dict == null)
                {
                    continue;
                }

                var profileObj = dict.GetPageObject(PdfTokens.DestOutputProfileKey);

                if (profileObj != null && !profileObj.StreamData.IsEmpty)
                {
                    var data = document.StreamDecoder.DecodeContentStream(profileObj);
                    if (!data.IsEmpty)
                    {
                        return data.ToArray();
                    }
                }
            }

            return null;
        }

        private static PdfColorSpaceConverter ResolveByNameAndValue(PdfString name, IPdfValue value, PdfPage page)
        {
            if (name.IsEmpty)
            {
                return DeviceRgbConverter.Instance;
            }

            var nameEnum = name.AsEnum<PdfColorSpace>();

            switch (nameEnum)
            {
                case PdfColorSpace.DeviceGray:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceGray, page);
                case PdfColorSpace.DeviceRGB:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceRGB, page);
                case PdfColorSpace.DeviceCMYK:
                    return ResolveDeviceConverter(PdfColorSpace.DeviceCMYK, page);
                case PdfColorSpace.Indexed:
                {
                    var conv = ColorSpaceFactory.CreateIndexedColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.ICCBased:
                {
                    var conv = ColorSpaceFactory.CreateIccColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.CalGray:
                {
                    var conv = ColorSpaceFactory.CreateCalGrayColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.CalRGB:
                {
                    var conv = ColorSpaceFactory.CreateCalRrgbColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Lab:
                {
                    var conv = ColorSpaceFactory.CreateLabColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Pattern:
                {
                    var conv = ColorSpaceFactory.CreatePatternColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpace.Separation:
                {
                    var conv = ColorSpaceFactory.CreateSeparationColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpace.DeviceN:
                {
                    var conv = ColorSpaceFactory.CreateDeviceNColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                default:
                {
                    var resVal = ResolveColorSpaceValue(name, page);
                    if (resVal != null)
                    {
                        return ResolveByValue(resVal, page);
                    }

                    return DeviceRgbConverter.Instance;
                }
            }
        }

        private static IPdfValue ResolveColorSpaceValue(PdfString resourceName, PdfPage page)
        {
            try
            {
                var resources = page.ResourceDictionary;
                var csDict = resources?.GetDictionary(PdfTokens.ColorSpaceKey);
                return csDict?.GetValue(resourceName);
            }
            catch
            {
                return null;
            }
        }

        public static bool TryGetColorSpaceName(IPdfValue value, out PdfString name)
        {
            if (value.Type == PdfValueType.Name)
            {
                name = value.AsName();
                return !name.IsEmpty;
            }
            else if (value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count > 0)
                {
                    name = array.GetName(0);
                    return !name.IsEmpty;
                }
            }

            name = default;
            return false;
        }

        public static bool TryGetColorSpaceObject(IPdfValue value, out PdfObject pdfObject)
        {
            if (value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count == 2)
                {
                    pdfObject = array.GetPageObject(1);
                    return pdfObject?.Reference.IsValid == true;
                }
            }

            pdfObject = null;
            return false;
        }

        public static bool TryResolveFromCache(PdfObject pdfObject, out PdfColorSpaceConverter value)
        {
            if (pdfObject.Reference.IsValid && pdfObject.Document.ColorSpaceConverters.TryGetValue(pdfObject.Reference, out var existing))
            {
                value = existing;
                return true;
            }
            value = null;
            return false;
        }

        public static bool TryStoreByReference(PdfObject pdfObject, PdfColorSpaceConverter converter)
        {
            if (pdfObject.Reference.IsValid)
            {
                pdfObject.Document.ColorSpaceConverters[pdfObject.Reference] = converter;
                return true;
            }
            return false;
        }
    }
}
