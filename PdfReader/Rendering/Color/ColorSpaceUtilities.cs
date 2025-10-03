using System;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    internal static class ColorSpaceUtilities
    {
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }
            return name[0] == '/' ? name.Substring(1) : name;
        }

        public static bool TryGetColorSpaceName(PdfPage page, IPdfValue value, out string name)
        {
            if (value.Type == PdfValueType.Reference)
            {
                return TryGetColorSpaceName(page, value.ResolveToNonReference(page.Document), out name);
            }
            else if (value.Type == PdfValueType.Name)
            {
                name = value.AsString();
                return !string.IsNullOrEmpty(name);
            }
            else if (value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count > 0)
                {
                    name = array[0].AsString();
                    return !string.IsNullOrEmpty(name);
                }
            }

            name = default;
            return false;
        }

        public static bool TryGetColorSpaceReference(PdfPage page, IPdfValue value, out PdfReference reference)
        {
            if (value.Type == PdfValueType.Reference)
            {
                reference = value.AsReference();
                return true;
            }
            else if (value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count == 2 && array[1].Type == PdfValueType.Reference)
                {
                    reference = array[1].AsReference();
                    return true;
                }
            }

            reference = default;
            return false;
        }

        public static bool TryResolveFromCache(PdfPage page, PdfReference reference, out PdfColorSpaceConverter value)
        {
            if (reference.IsValid && page.Document.ColorSpaceConverters.TryGetValue(reference, out var exsisting))
            {
                value = exsisting;
                return true;
            }
            value = null;
            return false;
        }

        public static bool TryStoreByReference(PdfPage page, PdfReference reference, PdfColorSpaceConverter converter)
        {
            if (reference.IsValid)
            {
                page.Document.ColorSpaceConverters[reference] = converter;
                return true;
            }
            return false;
        }
    }
}
