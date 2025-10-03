using System;
using PdfReader.Models;
using PdfReader.Fonts;

namespace PdfReader.Rendering.Color
{
    internal static class ColorSpaceFactory
    {
        // ------------------------------------------------------------------------------------
        // Indexed Color Space
        // [/Indexed base hival lookup]
        // lookup: string / hex string / stream (via indirect object)
        // ------------------------------------------------------------------------------------
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
            var baseConverter = PdfColorSpaces.ResolveByValue(baseCsValue, page);
            if (baseConverter == null)
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
                            // Ignore malformed lookup stream; leave empty lookup
                        }
                    }
                    break;
                }
            }

            return new IndexedConverter(baseConverter, hiVal, lookupBytes);
        }

        // ------------------------------------------------------------------------------------
        // Separation & DeviceN helpers
        // ------------------------------------------------------------------------------------
        private static PdfObject ResolveTintFunction(IPdfValue funcVal, PdfPage page)
        {
            if (funcVal == null)
            {
                return null;
            }

            // If the function itself is an indirect reference, return the referenced PdfObject.
            if (funcVal.Type == PdfValueType.Reference)
            {
                var r = funcVal.AsReference();
                page.Document.Objects.TryGetValue(r.ObjectNumber, out var objRef);
                return objRef;
            }

            if (funcVal.Type == PdfValueType.Dictionary)
            {
                // Inline function dictionary (no separate stream object). Wrap it.
                return new PdfObject(new PdfReference(0, 0), page.Document, funcVal);
            }

            if (funcVal.Type == PdfValueType.Array)
            {
                var arr = funcVal.AsArray();
                for (int i = 0; i < arr.Count; i++)
                {
                    var candidate = ResolveTintFunction(arr[i], page);
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        // ------------------------------------------------------------------------------------
        // Separation Color Space
        // [/Separation name altSpace tintFunc]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreateSeparationColorSpace(IPdfValue value, PdfPage page)
        {
            if (value == null)
            {
                return null;
            }

            // Use AsArray (after resolving only the top-level reference) so that element references remain intact.
            IPdfValue raw = value;
            if (raw.Type == PdfValueType.Reference)
            {
                raw = raw.ResolveToNonReference(page.Document);
            }
            var arr = raw.AsArray();
            if (arr == null || arr.Count < 4)
            {
                return null;
            }

            string name = arr[1].AsString();
            var alt = PdfColorSpaces.ResolveByValue(arr[2], page);
            var tintFunc = ResolveTintFunction(arr[3], page);
            return new SeparationColorSpaceConverter(name, alt, tintFunc);
        }

        // ------------------------------------------------------------------------------------
        // DeviceN Color Space
        // [/DeviceN [names] altSpace tintTransform (attributes)]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreateDeviceNColorSpace(IPdfValue value, PdfPage page)
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
            var arr = raw.AsArray();
            if (arr == null || arr.Count < 4)
            {
                return null;
            }

            var namesArray = arr[1].AsArray();
            if (namesArray == null || namesArray.Count == 0)
            {
                return null;
            }
            var names = new string[namesArray.Count];
            for (int i = 0; i < namesArray.Count; i++)
            {
                names[i] = namesArray[i].AsString();
            }

            var alt = PdfColorSpaces.ResolveByValue(arr[2], page);
            var tintFunc = ResolveTintFunction(arr[3], page);
            return new DeviceNColorSpaceConverter(names, alt, tintFunc);
        }

        // ------------------------------------------------------------------------------------
        // CalGray Color Space
        // [/CalGray << /WhitePoint ... /BlackPoint ... /Gamma ... >>]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreateCalGrayColorSpace(IPdfValue value, PdfPage page)
        {
            var dict = GetDictionaryValue(value, page);
            if (dict == null)
            {
                return null;
            }

            float xw = 0.9505f;
            float yw = 1.0f;
            float zw = 1.0890f;
            var wp = dict.GetArray(PdfTokens.WhitePointKey);
            if (wp != null && wp.Count >= 3)
            {
                xw = wp[0].AsFloat();
                yw = wp[1].AsFloat();
                zw = wp[2].AsFloat();
            }

            float gamma = 1.0f;
            if (dict.TryGetFloat(PdfTokens.GammaKey, out var gSingle))
            {
                gamma = gSingle;
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

        // ------------------------------------------------------------------------------------
        // CalRGB Color Space
        // [/CalRGB << /WhitePoint [...] /BlackPoint [...] /Gamma [...] /Matrix [...] >>]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreateCalRrgbColorSpace(IPdfValue value, PdfPage page)
        {
            var dict = GetDictionaryValue(value, page);
            if (dict == null)
            {
                return null;
            }

            float xw = 0.9505f;
            float yw = 1.0f;
            float zw = 1.0890f;
            var wp = dict.GetArray(PdfTokens.WhitePointKey);
            if (wp != null && wp.Count >= 3)
            {
                xw = wp[0].AsFloat();
                yw = wp[1].AsFloat();
                zw = wp[2].AsFloat();
            }

            float gr = 1.0f;
            float gg = 1.0f;
            float gb = 1.0f;
            if (dict.TryGetFloat(PdfTokens.GammaKey, out var gSingle))
            {
                gr = gg = gb = gSingle;
            }
            else
            {
                var gArr = dict.GetArray(PdfTokens.GammaKey);
                if (gArr != null && gArr.Count >= 3)
                {
                    gr = gArr[0].AsFloat();
                    gg = gArr[1].AsFloat();
                    gb = gArr[2].AsFloat();
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

            float[,] matrix = new float[3, 3]
            {
                { 1f, 0f, 0f },
                { 0f, 1f, 0f },
                { 0f, 0f, 1f }
            };
            var mat = dict.GetArray(PdfTokens.MatrixKey);
            if (mat != null && mat.Count >= 9)
            {
                matrix[0, 0] = mat[0].AsFloat();
                matrix[0, 1] = mat[1].AsFloat();
                matrix[0, 2] = mat[2].AsFloat();
                matrix[1, 0] = mat[3].AsFloat();
                matrix[1, 1] = mat[4].AsFloat();
                matrix[1, 2] = mat[5].AsFloat();
                matrix[2, 0] = mat[6].AsFloat();
                matrix[2, 1] = mat[7].AsFloat();
                matrix[2, 2] = mat[8].AsFloat();
            }

            return new CalRgbConverter(xw, yw, zw, xb, yb, zb, gr, gg, gb, matrix);
        }

        // ------------------------------------------------------------------------------------
        // ICCBased Color Space
        // [/ICCBased obj] where obj stream has /N and optional /Alternate and profile bytes
        // ------------------------------------------------------------------------------------
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

        // ------------------------------------------------------------------------------------
        // Pattern Color Space
        // [/Pattern]  or  [/Pattern baseCS]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreatePatternColorSpace(IPdfValue value, PdfPage page)
        {
            if (value != null && value.Type == PdfValueType.Array)
            {
                var array = value.AsArray();
                if (array.Count >= 2)
                {
                    var baseConv = PdfColorSpaces.ResolveByValue(array[1], page);
                    return new PatternColorSpaceConverter(baseConv);
                }
            }
            return new PatternColorSpaceConverter(null);
        }

        // ------------------------------------------------------------------------------------
        // Lab Color Space
        // [/Lab << /WhitePoint [...] /Range [...] /BlackPoint [...] >>]
        // ------------------------------------------------------------------------------------
        public static PdfColorSpaceConverter CreateLabColorSpace(IPdfValue value, PdfPage page)
        {
            var dict = GetDictionaryValue(value, page);
            if (dict == null)
            {
                return null;
            }

            float xw = 0.9642f;
            float yw = 1.0f;
            float zw = 0.8249f;
            var wpArr = dict.GetArray(PdfTokens.WhitePointKey);
            if (wpArr != null && wpArr.Count >= 3)
            {
                xw = wpArr[0].AsFloat();
                yw = wpArr[1].AsFloat();
                zw = wpArr[2].AsFloat();
            }
            if (yw <= 0f)
            {
                yw = 1.0f;
            }

            float aMin = -100f;
            float aMax = 100f;
            float bMin = -100f;
            float bMax = 100f;
            var rangeArr = dict.GetArray(PdfTokens.RangeKey);
            if (rangeArr != null && rangeArr.Count >= 4)
            {
                aMin = rangeArr[0].AsFloat();
                aMax = rangeArr[1].AsFloat();
                bMin = rangeArr[2].AsFloat();
                bMax = rangeArr[3].AsFloat();
            }

            return new LabColorSpaceConverter(xw, yw, zw, aMin, aMax, bMin, bMax);
        }

        // ------------------------------------------------------------------------------------
        // Utility helpers for dictionary / object resolution based on standard array forms
        // ------------------------------------------------------------------------------------
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
