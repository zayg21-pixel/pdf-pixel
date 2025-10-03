using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    public static class PdfColorSpaces
    {
        public static PdfColorSpaceConverter ResolveByValue(IPdfValue value, PdfPage page)
        {
            if (value == null) return DeviceRgbConverter.Instance;

            if (!ColorSpaceUtilities.TryGetColorSpaceName(page, value, out var name))
            {
                return DeviceRgbConverter.Instance;
            }

            bool hasReference = ColorSpaceUtilities.TryGetColorSpaceReference(page, value, out var reference);

            if (hasReference && ColorSpaceUtilities.TryResolveFromCache(page, reference, out var cached))
            {
                return cached;
            }

            var result = ResolveByNameAndValue(name, value, page);

            if (hasReference)
            {
                ColorSpaceUtilities.TryStoreByReference(page, reference, result);
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
                var doc = page?.Document;
                if (doc == null || doc.RootRef == 0 || !doc.Objects.TryGetValue(doc.RootRef, out var root))
                {
                    return null;
                }

                var catalog = root.Dictionary;
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
                        var data = PdfStreamDecoder.DecodeContentStream(profileObj);
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
                case PdfColorSpaceNames.Pattern:
                {
                    var conv = ColorSpaceFactory.CreatePatternColorSpace(value, page);
                    return conv ?? DeviceRgbConverter.Instance;
                }
                default:
                {
                    var resVal = ResolveColorSpaceValue(name, page);
                    if (resVal != null)
                    {
                        return ResolveByValue(resVal, page);
                    }
                    Console.WriteLine("[ColorSpaces] NOT FULLY IMPLEMENTED: Unknown or unsupported color space '" + name + "'. Falling back to DeviceRGB.");
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

        public static PdfColorSpace ParseColorSpaceName(string colorSpaceName)
        {
            colorSpaceName = ColorSpaceUtilities.NormalizeName(colorSpaceName);

            switch (colorSpaceName)
            {
                case PdfColorSpaceNames.DeviceGray: return PdfColorSpace.DeviceGray;
                case PdfColorSpaceNames.DeviceRGB: return PdfColorSpace.DeviceRGB;
                case PdfColorSpaceNames.DeviceCMYK: return PdfColorSpace.DeviceCMYK;
                case PdfColorSpaceNames.ICCBased: return PdfColorSpace.ICCBased;
                case PdfColorSpaceNames.Indexed: return PdfColorSpace.Indexed;
                case PdfColorSpaceNames.Lab: return PdfColorSpace.Lab; // Not implemented
                case PdfColorSpaceNames.CalGray: return PdfColorSpace.CalGray;
                case PdfColorSpaceNames.CalRGB: return PdfColorSpace.CalRGB;
                case PdfColorSpaceNames.Pattern: return PdfColorSpace.Pattern;
                default: return PdfColorSpace.Unknown;
            }
        }
    }
}
