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

        public static bool TryGetColorSpaceName(IPdfValue value, out string name)
        {
            if (value.Type == PdfValueType.Name)
            {
                name = value.AsName();
                return !string.IsNullOrEmpty(name);
            }
            else if (value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count > 0)
                {
                    name = array.GetName(0);
                    return !string.IsNullOrEmpty(name);
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
