using PdfReader.Fonts;
using PdfReader.Models;
using System;

namespace PdfReader.Rendering.Color
{
    internal static partial class ColorSpaceFactory
    {
        public static PdfColorSpaceConverter CreateIndexedColorSpace(IPdfValue value, PdfPage page)
        {
            if (value == null)
            {
                return null;
            }

            IPdfValue raw = value;
            if (raw.Type == PdfValueType.Reference)
            {
                raw = raw.ResolveToNonReference(page.Document);
            }

            var array = raw.AsArray();
            if (array == null || array.Count < 4)
            {
                return null;
            }

            var baseCsValue = array[1];
            var baseConv = PdfColorSpaces.ResolveByValue(baseCsValue, page);
            if (baseConv == null)
            {
                return null;
            }

            int hiVal;
            try
            {
                hiVal = array[2].AsInteger();
            }
            catch
            {
                hiVal = 0;
            }
            if (hiVal < 0)
            {
                hiVal = 0;
            }

            byte[] lookupBytes = Array.Empty<byte>();
            var lutVal = array[3];
            switch (lutVal.Type)
            {
                case PdfValueType.HexString:
                    lookupBytes = lutVal.AsHexBytes() ?? Array.Empty<byte>();
                    break;
                case PdfValueType.String:
                {
                    var s = lutVal.AsString() ?? string.Empty;
                    lookupBytes = EncodingExtensions.PdfDefault.GetBytes(s);
                    break;
                }
                case PdfValueType.Reference:
                {
                    var r = lutVal.AsReference();
                    if (page.Document.Objects.TryGetValue(r.ObjectNumber, out var obj) && obj != null)
                    {
                        try
                        {
                            var data = PdfStreamDecoder.DecodeContentStream(obj);
                            if (!data.IsEmpty)
                            {
                                lookupBytes = data.ToArray();
                            }
                        }
                        catch
                        {
                            // Ignored: malformed lookup stream, fallback to zeros.
                        }
                    }
                    break;
                }
            }

            return new IndexedConverter(baseConv, hiVal, lookupBytes);
        }

        public static PdfColorSpaceConverter CreateCalGrayColorSpace(IPdfValue value, PdfPage page)
        {
            var dict = GetDictionaryValue(value, page);
            if (dict == null)
            {
                return null;
            }

            float xw = 0.9505f, yw = 1.0f, zw = 1.0890f;
            var wp = dict.GetArray(PdfTokens.WhitePointKey);
            if (wp != null && wp.Count >= 3)
            {
                xw = wp[0].AsFloat();
                yw = wp[1].AsFloat();
                zw = wp[2].AsFloat();
            }

            float gamma = 1.0f;
            if (dict.TryGetFloat(PdfTokens.GammaKey, out var gsingle))
            {
                gamma = gsingle;
            }

            float xb = 0f, yb = 0f, zb = 0f;
            var bp = dict.GetArray(PdfTokens.BlackPointKey);
            if (bp != null && bp.Count >= 3)
            {
                xb = bp[0].AsFloat();
                yb = bp[1].AsFloat();
                zb = bp[2].AsFloat();
            }

            return new CalGrayConverter(xw, yw, zw, xb, yb, zb, gamma);
        }

        public static PdfColorSpaceConverter CreateCalRrgbColorSpace(IPdfValue value, PdfPage page)
        {
            var dict = GetDictionaryValue(value, page);
            if (dict == null)
            {
                return null;
            }

            float xw = 0.9505f, yw = 1.0f, zw = 1.0890f;
            var wp = dict.GetArray(PdfTokens.WhitePointKey);
            if (wp != null && wp.Count >= 3)
            {
                xw = wp[0].AsFloat();
                yw = wp[1].AsFloat();
                zw = wp[2].AsFloat();
            }

            float gr = 1.0f, gg = 1.0f, gb = 1.0f;
            if (dict.TryGetFloat(PdfTokens.GammaKey, out var gsingle))
            {
                gr = gg = gb = gsingle;
            }
            else
            {
                var garr = dict.GetArray(PdfTokens.GammaKey);
                if (garr != null && garr.Count >= 3)
                {
                    gr = garr[0].AsFloat();
                    gg = garr[1].AsFloat();
                    gb = garr[2].AsFloat();
                }
            }

            float xb = 0f, yb = 0f, zb = 0f;
            var bp = dict.GetArray(PdfTokens.BlackPointKey);
            if (bp != null && bp.Count >= 3)
            {
                xb = bp[0].AsFloat();
                yb = bp[1].AsFloat();
                zb = bp[2].AsFloat();
            }

            float[,] m = new float[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            var mat = dict.GetArray(PdfTokens.MatrixKey);
            if (mat != null && mat.Count >= 9)
            {
                m[0, 0] = mat[0].AsFloat(); m[0, 1] = mat[1].AsFloat(); m[0, 2] = mat[2].AsFloat();
                m[1, 0] = mat[3].AsFloat(); m[1, 1] = mat[4].AsFloat(); m[1, 2] = mat[5].AsFloat();
                m[2, 0] = mat[6].AsFloat(); m[2, 1] = mat[7].AsFloat(); m[2, 2] = mat[8].AsFloat();
            }

            return new CalRgbConverter(xw, yw, zw, xb, yb, zb, gr, gg, gb, m);
        }

        public static PdfColorSpaceConverter CreateIccColorSpace(IPdfValue value, PdfPage page)
        {
            var pdfObject = GetObjectValue(value, page);
            if (pdfObject == null)
            {
                return null;
            }

            int n = 3;
            PdfColorSpaceConverter alt = null;
            var dict = pdfObject.Dictionary;
            n = dict.GetInteger(PdfTokens.NKey);
            var altVal = dict.GetValue(PdfTokens.AlternateKey);
            if (altVal != null)
            {
                alt = PdfColorSpaces.ResolveByValue(altVal, page);
            }

            byte[] iccBytes = null;
            if (!pdfObject.StreamData.IsEmpty)
            {
                var data = PdfStreamDecoder.DecodeContentStream(pdfObject);
                iccBytes = data.ToArray();
            }

            return new IccBasedConverter(n, alt, iccBytes);
        }

        public static PdfColorSpaceConverter CreatePatternColorSpace(IPdfValue value, PdfPage page)
        {
            // Caller guarantees this represents a Pattern color space. Only array form [/Pattern base] needs inspection.
            if (value != null && value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count >= 2)
                {
                    var baseConv = PdfColorSpaces.ResolveByValue(array[1], page);
                    return new PatternColorSpaceConverter(baseConv);
                }
            }
            // Colored pattern (no base) or malformed array -> default to colored pattern semantics
            return new PatternColorSpaceConverter(null);
        }

        private static PdfDictionary GetDictionaryValue(IPdfValue value, PdfPage page)
        {
            if (value == null)
            {
                return null;
            }
            var parameters = value.ResolveToArray(page.Document);
            if (parameters == null || parameters.Count < 2)
            {
                return null;
            }
            return parameters[1].AsDictionary();
        }

        private static PdfObject GetObjectValue(IPdfValue value, PdfPage page)
        {
            if (value == null)
            {
                return null;
            }
            if (value.Type == PdfValueType.Reference)
            {
                return GetObjectValue(value.ResolveToNonReference(page.Document), page);
            }
            var parameters = value.AsArray();
            if (parameters == null || parameters.Count < 2)
            {
                return null;
            }
            var reference = parameters[1].AsReference();
            if (reference.IsValid && page.Document.Objects.TryGetValue(reference.ObjectNumber, out var pdfObject))
            {
                return pdfObject;
            }
            return null;
        }
    }
}
