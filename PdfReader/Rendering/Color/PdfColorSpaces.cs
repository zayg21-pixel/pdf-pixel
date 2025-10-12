using System;
using PdfReader.Models;
using PdfReader.Streams;

namespace PdfReader.Rendering.Color
{
    public static class PdfColorSpaces
    {
        public static PdfColorSpaceConverter ResolveByValue(IPdfValue value, PdfPage page)
        {
            if (value == null) return DeviceRgbConverter.Instance;

            if (!ColorSpaceUtilities.TryGetColorSpaceName(value, out var name))
            {
                return DeviceRgbConverter.Instance;
            }

            bool hasReference = ColorSpaceUtilities.TryGetColorSpaceObject(value, out var pdfObject);

            if (hasReference && ColorSpaceUtilities.TryResolveFromCache(pdfObject, out var cached))
            {
                return cached;
            }

            var result = ResolveByNameAndValue(name, value, page);

            if (hasReference)
            {
                ColorSpaceUtilities.TryStoreByReference(pdfObject, result);
            }

            return result;
        }

        public static PdfColorSpaceConverter ResolveDeviceConverter(string name, PdfPage page)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DeviceRgbConverter.Instance;
            }

            switch (name)
            {
                case PdfColorSpaceNames.DeviceGray:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultGrayKey, 1) ?? DeviceGrayConverter.Instance;
                case PdfColorSpaceNames.DeviceRGB:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultRGBKey, 3) ?? DeviceRgbConverter.Instance;
                case PdfColorSpaceNames.DeviceCMYK:
                    return ResolveDefaultDeviceSpace(page, PdfTokens.DefaultCMYKKey, 4) ?? DeviceCmykConverter.Instance;
            }

            return DeviceRgbConverter.Instance;
        }

        private static PdfColorSpaceConverter ResolveDefaultDeviceSpace(PdfPage page, string defaultKey, int n)
        {
            try
            {
                var resources = page?.ResourceDictionary;
                var defaultVal = resources?.GetValue(defaultKey);
                if (defaultVal != null)
                {
                    // Default.* can be an ICCBased space or other color space; reuse existing parser
                    var conv = ResolveByValue(defaultVal, page);
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
                var profileBytes = TryGetFirstOutputIntentProfile(page);
                if (profileBytes != null && profileBytes.Length > 0)
                {
                    return new IccBasedConverter(n, null, profileBytes);
                }
            }
            catch
            {
                // Safe to ignore; fall back to naive device space
            }

            return null;
        }

        private static byte[] TryGetFirstOutputIntentProfile(PdfPage page)
        {
            try
            {
                var rootObject = page.Document.RootObject;
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
                        var data = page.Document.StreamDecoder.DecodeContentStream(profileObj);
                        if (!data.IsEmpty)
                        {
                            return data.ToArray();
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static PdfColorSpaceConverter ResolveByNameAndValue(string name, IPdfValue value, PdfPage page)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DeviceRgbConverter.Instance;
            }

            switch (name)
            {
                case PdfColorSpaceNames.DeviceGray:
                    return ResolveDeviceConverter(name, page);
                case PdfColorSpaceNames.DeviceRGB:
                    return ResolveDeviceConverter(name, page);
                case PdfColorSpaceNames.DeviceCMYK:
                    return ResolveDeviceConverter(name, page);
                case PdfColorSpaceNames.Indexed:
                {
                    var conv = ColorSpaceFactory.CreateIndexedColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpaceNames.ICCBased:
                {
                    var conv = ColorSpaceFactory.CreateIccColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpaceNames.CalGray:
                {
                    var conv = ColorSpaceFactory.CreateCalGrayColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpaceNames.CalRGB:
                {
                    var conv = ColorSpaceFactory.CreateCalRrgbColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpaceNames.Lab:
                {
                    var conv = ColorSpaceFactory.CreateLabColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpaceNames.Pattern:
                {
                    var conv = ColorSpaceFactory.CreatePatternColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                case PdfColorSpaceNames.Separation:
                {
                    var conv = ColorSpaceFactory.CreateSeparationColorSpace(value, page);
                    return conv ?? DeviceGrayConverter.Instance;
                }
                case PdfColorSpaceNames.DeviceN:
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

        private static IPdfValue ResolveColorSpaceValue(string resourceName, PdfPage page)
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
    }
}
