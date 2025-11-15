using System;
using PdfReader.Models;
using PdfReader.Rendering.Functions;
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
            var baseConverter = page.Cache.ColorSpace.ResolveByValue(baseCsValue);
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
                var data = page.Document.StreamDecoder.DecodeContentStream(lutObject);

                if (!data.IsEmpty)
                {
                    lookupBytes = data.ToArray();
                }
                else
                {
                    var lutVal = lutObject.Value;
                    lookupBytes = lutVal.AsStringBytes().ToArray();
                }
            }

            return new IndexedConverter(baseConverter, hiVal, lookupBytes);
        }

        // ------------------------------------------------------------------------------------
        // Separation & DeviceN helpers
        // ------------------------------------------------------------------------------------
        /// <summary>
        /// Resolve a tint transform function definition to a single PdfFunction, as required by the PDF spec.
        /// Correct resolution order:
        /// 1. Attempt to extract an indirect object via <see cref="PdfArray.GetPageObject"/> at the specified index.
        /// 2. Fall back to inline dictionary (wrapped as a PdfObject with a dummy reference).
        /// </summary>
        /// <param name="array">Parent color space parameter array.</param>
        /// <param name="index">Index of the tint transform entry.</param>
        /// <param name="page">Owning page (for document reference).</param>
        /// <returns>Resolved PdfFunction or null if none found.</returns>
        private static PdfFunction ResolveTintFunction(PdfArray array, int index, PdfPage page)
        {
            if (array == null)
            {
                return null;
            }

            var directObject = array.GetPageObject(index);
            if (directObject != null)
            {
                return PdfFunctions.GetFunction(directObject);
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

            var name = arr.GetName(1);
            var alt = page.Cache.ColorSpace.ResolveByValue(arr.GetValue(2));
            var tintFunction = ResolveTintFunction(arr, 3, page);
            return new SeparationColorSpaceConverter(name, alt, tintFunction);
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

            var namesArray = arr.GetArray(1);
            if (namesArray == null || namesArray.Count == 0)
            {
                return null;
            }
            var names = new PdfString[namesArray.Count];
            for (int index = 0; index < namesArray.Count; index++)
            {
                names[index] = namesArray.GetString(index);
            }

            var alt = page.Cache.ColorSpace.ResolveByValue(arr.GetValue(2));
            var tintFunction = ResolveTintFunction(arr, 3, page);
            return new DeviceNColorSpaceConverter(names, alt, tintFunction);
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


            var whitePoint = dict.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            var blackPoint = dict.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
            var gamma = dict.GetFloat(PdfTokens.GammaKey);

            return new CalGrayConverter(whitePoint, blackPoint, gamma);
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

            var whitePoint = dict.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();

            float[] gamma = null;
            var gSingle = dict.GetFloat(PdfTokens.GammaKey);

            if (gSingle.HasValue)
            {
                gamma = [gSingle.Value, gSingle.Value, gSingle.Value];
            }
            else
            {
                var gArr = dict.GetArray(PdfTokens.GammaKey)?.GetFloatArray();
                gamma = gArr;
            }

            var blackPoint = dict.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();


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

            return new CalRgbConverter(whitePoint, blackPoint, gamma, matrix);
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
            var altVal = page.Cache.ColorSpace.ResolveByValue(dict.GetValue(PdfTokens.AlternateKey), n);

            byte[] iccBytes = null;
            if (pdfObject.HasStream)
            {
                var data = page.Document.StreamDecoder.DecodeContentStream(pdfObject);
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
                    var baseConv = page.Cache.ColorSpace.ResolveByValue(arrayValue);
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
