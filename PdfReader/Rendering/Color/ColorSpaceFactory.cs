using System;
using PdfReader.Models;
using PdfReader.Streams;
using PdfReader.Text;

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

            var array = value.AsArray();
            if (array == null || array.Count < 4)
            {
                return null;
            }

            var baseCsValue = array.GetValue(1);
            var baseConverter = PdfColorSpaces.ResolveByValue(baseCsValue, page);
            if (baseConverter == null)
            {
                return null;
            }

            int hiVal = array.GetInteger(2);

            if (hiVal < 0)
            {
                hiVal = 0;
            }

            byte[] lookupBytes = Array.Empty<byte>();

            var lutObject = array.GetPageObject(3);

            if (lutObject != null)
            {
                var data = PdfStreamDecoder.DecodeContentStream(lutObject);

                if (!data.IsEmpty)
                {
                    lookupBytes = data.ToArray();
                }
            }
            else
            {
                var lutVal = array.GetValue(3);

                switch (lutVal?.Type)
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
                }
            }

            return new IndexedConverter(baseConverter, hiVal, lookupBytes);
        }

        // ------------------------------------------------------------------------------------
        // Separation & DeviceN helpers
        // ------------------------------------------------------------------------------------
        /// <summary>
        /// Resolve a tint transform function definition to a PdfObject.
        /// Correct resolution order:
        /// 1. Attempt to extract an indirect object via <see cref="PdfArray.GetPageObject"/> at the specified index.
        /// 2. Fall back to inline dictionary (wrapped as a PdfObject with a dummy reference).
        /// 3. If the entry is an array of functions, search each element recursively (same ordering per element).
        /// This replaces the previous implementation that incorrectly used GetValue first (losing object stream context).
        /// </summary>
        /// <param name="array">Parent color space parameter array.</param>
        /// <param name="index">Index of the tint transform entry.</param>
        /// <param name="page">Owning page (for document reference).</param>
        /// <returns>Resolved PdfObject or null if none found.</returns>
        private static PdfObject ResolveTintFunction(PdfArray array, int index, PdfPage page)
        {
            if (array == null)
            {
                return null;
            }

            // First attempt: try to obtain a page object (indirect reference or inline stream wrapper).
            var directObject = array.GetPageObject(index);
            if (directObject != null)
            {
                return directObject;
            }

            // Fallback to raw value for further inspection.
            var rawValue = array.GetValue(index);
            return ResolveTintFunction(rawValue, page);
        }

        /// <summary>
        /// Recursive helper for resolving tint function when starting from a generic value (array or dictionary).
        /// Keeps original semantics but is now only used internally after object extraction attempts.
        /// </summary>
        /// <param name="funcVal">Candidate value.</param>
        /// <param name="page">Owning page.</param>
        /// <returns>PdfObject wrapping the function dictionary or null.</returns>
        private static PdfObject ResolveTintFunction(IPdfValue funcVal, PdfPage page)
        {
            if (funcVal == null)
            {
                return null;
            }

            // Inline dictionary case: wrap it so callers uniformly receive PdfObject.
            if (funcVal.Type == PdfValueType.Dictionary)
            {
                return new PdfObject(new PdfReference(0, 0), page.Document, funcVal);
            }

            // Array case: search elements using same object-first strategy.
            if (funcVal.Type == PdfValueType.Array)
            {
                var pdfArray = funcVal.AsArray();
                if (pdfArray != null)
                {
                    for (int elementIndex = 0; elementIndex < pdfArray.Count; elementIndex++)
                    {
                        var elementObject = pdfArray.GetPageObject(elementIndex);
                        if (elementObject != null)
                        {
                            return elementObject;
                        }
                        var innerValue = pdfArray.GetValue(elementIndex);
                        var wrapped = ResolveTintFunction(innerValue, page);
                        if (wrapped != null)
                        {
                            return wrapped;
                        }
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

            var arr = value.AsArray();

            if (arr == null || arr.Count < 4)
            {
                return null;
            }

            string name = arr.GetString(1);
            var alt = PdfColorSpaces.ResolveByValue(arr.GetValue(2), page);
            var tintFunc = ResolveTintFunction(arr, 3, page);
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

            var arr = value.AsArray();

            if (arr == null || arr.Count < 4)
            {
                return null;
            }

            var namesArray = arr.GetValue(1)?.AsArray();
            if (namesArray == null || namesArray.Count == 0)
            {
                return null;
            }
            var names = new string[namesArray.Count];
            for (int index = 0; index < namesArray.Count; index++)
            {
                names[index] = namesArray.GetString(index);
            }

            var alt = PdfColorSpaces.ResolveByValue(arr.GetValue(2), page);
            var tintFunc = ResolveTintFunction(arr, 3, page);
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
            var wp = dict.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            if (wp != null && wp.Length >= 3)
            {
                xw = wp[0];
                yw = wp[1];
                zw = wp[2];
            }

            float gamma = 1.0f;
            var gSingle = dict.GetFloat(PdfTokens.GammaKey);
            if (gSingle.HasValue)
            {
                gamma = gSingle.Value;
            }

            float xb = 0f;
            float yb = 0f;
            float zb = 0f;
            var bp = dict.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
            if (bp != null && bp.Length >= 3)
            {
                xb = bp[0];
                yb = bp[1];
                zb = bp[2];
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
            var wp = dict.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            if (wp != null && wp.Length >= 3)
            {
                xw = wp[0];
                yw = wp[1];
                zw = wp[2];
            }

            float gr = 1.0f;
            float gg = 1.0f;
            float gb = 1.0f;
            var gSingle = dict.GetFloat(PdfTokens.GammaKey);

            if (gSingle.HasValue)
            {
                gr = gSingle.Value;
                gg = gSingle.Value;
                gb = gSingle.Value;
            }
            else
            {
                var gArr = dict.GetArray(PdfTokens.GammaKey)?.GetFloatArray();
                if (gArr != null && gArr.Length >= 3)
                {
                    gr = gArr[0];
                    gg = gArr[1];
                    gb = gArr[2];
                }
            }

            float xb = 0f;
            float yb = 0f;
            float zb = 0f;
            var bp = dict.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
            if (bp != null && bp.Length >= 3)
            {
                xb = bp[0];
                yb = bp[1];
                zb = bp[2];
            }

            float[,] matrix = new float[3, 3]
            {
                { 1f, 0f, 0f },
                { 0f, 1f, 0f },
                { 0f, 0f, 1f }
            };

            var mat = dict.GetArray(PdfTokens.MatrixKey)?.GetFloatArray();
            if (mat != null && mat.Length >= 9)
            {
                matrix[0, 0] = mat[0];
                matrix[0, 1] = mat[1];
                matrix[0, 2] = mat[2];
                matrix[1, 0] = mat[3];
                matrix[1, 1] = mat[4];
                matrix[1, 2] = mat[5];
                matrix[2, 0] = mat[6];
                matrix[2, 1] = mat[7];
                matrix[2, 2] = mat[8];
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
            n = dict.GetIntegerOrDefault(PdfTokens.NKey);
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
                if (array != null && array.Count >= 2)
                {
                    var arrayValue = array.GetValue(1);
                    var baseConv = PdfColorSpaces.ResolveByValue(arrayValue, page);
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
            var wpArr = dict.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            if (wpArr != null && wpArr.Length >= 3)
            {
                xw = wpArr[0];
                yw = wpArr[1];
                zw = wpArr[2];
            }
            if (yw <= 0f)
            {
                yw = 1.0f;
            }

            float aMin = -100f;
            float aMax = 100f;
            float bMin = -100f;
            float bMax = 100f;
            var rangeArr = dict.GetArray(PdfTokens.RangeKey)?.GetFloatArray();
            if (rangeArr != null && rangeArr.Length >= 4)
            {
                aMin = rangeArr[0];
                aMax = rangeArr[1];
                bMin = rangeArr[2];
                bMax = rangeArr[3];
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

            var parameters = value.AsArray();
            if (parameters == null || parameters.Count < 2)
            {
                return null;
            }

            return parameters.GetDictionary(1);
        }

        private static PdfObject GetObjectValue(IPdfValue value, PdfPage page)
        {
            if (value == null)
            {
                return null;
            }

            var parameters = value.AsArray();
            if (parameters == null || parameters.Count < 2)
            {
                return null;
            }

            return parameters.GetPageObject(1);
        }
    }
}
