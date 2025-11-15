using System;
using PdfReader.Functions;
using PdfReader.Models;

namespace PdfReader.Color.ColorSpace
{
    /// <summary>
    /// Color space resolver factory methods.
    /// </summary>
    partial class ColorSpaceResolver
    {
        private PdfColorSpaceConverter CreateIndexedColorSpace(IPdfValue colorSpaceValue)
        {
            if (colorSpaceValue == null)
            {
                return null;
            }

            var colorSpaceArray = colorSpaceValue.AsArray();
            if (colorSpaceArray == null || colorSpaceArray.Count < 4)
            {
                return null;
            }

            var baseColorSpaceValue = colorSpaceArray.GetValue(1);
            var baseConverter = ResolveByValue(baseColorSpaceValue);
            if (baseConverter == null)
            {
                return null;
            }

            int highestIndex = colorSpaceArray.GetInteger(2);
            if (highestIndex < 0)
            {
                highestIndex = 0;
            }

            byte[] lookupTableBytes = Array.Empty<byte>();

            var lookupObject = colorSpaceArray.GetPageObject(3);
            if (lookupObject != null)
            {
                var lookupData = lookupObject.DecodeAsMemory();
                if (!lookupData.IsEmpty)
                {
                    lookupTableBytes = lookupData.ToArray();
                }
                else
                {
                    var lookupValue = lookupObject.Value;
                    lookupTableBytes = lookupValue.AsStringBytes().ToArray();
                }
            }

            return new IndexedConverter(baseConverter, highestIndex, lookupTableBytes);
        }

        private PdfColorSpaceConverter CreateSeparationColorSpace(IPdfValue colorSpaceValue)
        {
            if (colorSpaceValue == null)
            {
                return null;
            }

            var colorSpaceArray = colorSpaceValue.AsArray();
            if (colorSpaceArray == null || colorSpaceArray.Count < 4)
            {
                return null;
            }

            var colorantName = colorSpaceArray.GetName(1);
            var alternateConverter = ResolveByValue(colorSpaceArray.GetValue(2));
            var tintFunction = ResolveTintFunction(colorSpaceArray, 3);
            return new SeparationColorSpaceConverter(colorantName, alternateConverter, tintFunction);
        }

        private PdfColorSpaceConverter CreateDeviceNColorSpace(IPdfValue colorSpaceValue)
        {
            if (colorSpaceValue == null)
            {
                return null;
            }

            var colorSpaceArray = colorSpaceValue.AsArray();
            if (colorSpaceArray == null || colorSpaceArray.Count < 4)
            {
                return null;
            }

            var namesArray = colorSpaceArray.GetArray(1);
            if (namesArray == null || namesArray.Count == 0)
            {
                return null;
            }
            var colorantNames = new PdfString[namesArray.Count];
            for (int i = 0; i < namesArray.Count; i++)
            {
                colorantNames[i] = namesArray.GetString(i);
            }

            var alternateConverter = ResolveByValue(colorSpaceArray.GetValue(2));
            var tintFunction = ResolveTintFunction(colorSpaceArray, 3);
            return new DeviceNColorSpaceConverter(colorantNames, alternateConverter, tintFunction);
        }

        private PdfColorSpaceConverter CreateCalGrayColorSpace(IPdfValue colorSpaceValue)
        {
            var dictionary = GetDictionaryValue(colorSpaceValue);
            if (dictionary == null)
            {
                return null;
            }


            var whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            var blackPoint = dictionary.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
            var gamma = dictionary.GetFloat(PdfTokens.GammaKey);

            return new CalGrayConverter(whitePoint, blackPoint, gamma);
        }

        private PdfColorSpaceConverter CreateCalRrgbColorSpace(IPdfValue colorSpaceValue)
        {
            var dictionary = GetDictionaryValue(colorSpaceValue);
            if (dictionary == null)
            {
                return null;
            }

            var whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();

            float[] gamma = null;
            var gammaSingle = dictionary.GetFloat(PdfTokens.GammaKey);

            if (gammaSingle.HasValue)
            {
                gamma = [gammaSingle.Value, gammaSingle.Value, gammaSingle.Value];
            }
            else
            {
                var gammaArray = dictionary.GetArray(PdfTokens.GammaKey)?.GetFloatArray();
                gamma = gammaArray;
            }

            var blackPoint = dictionary.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();


            float[,] matrix = new float[3, 3]
            {
                { 1f, 0f, 0f },
                { 0f, 1f, 0f },
                { 0f, 0f, 1f }
            };

            var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey)?.GetFloatArray();
            if (matrixArray != null && matrixArray.Length >= 9)
            {
                matrix[0, 0] = matrixArray[0];
                matrix[0, 1] = matrixArray[1];
                matrix[0, 2] = matrixArray[2];
                matrix[1, 0] = matrixArray[3];
                matrix[1, 1] = matrixArray[4];
                matrix[1, 2] = matrixArray[5];
                matrix[2, 0] = matrixArray[6];
                matrix[2, 1] = matrixArray[7];
                matrix[2, 2] = matrixArray[8];
            }

            return new CalRgbConverter(whitePoint, blackPoint, gamma, matrix);
        }

        private PdfColorSpaceConverter CreateIccColorSpace(IPdfValue colorSpaceValue)
        {
            var pdfObject = GetObjectValue(colorSpaceValue);
            if (pdfObject == null)
            {
                return null;
            }

            int componentCount = 3;
            PdfColorSpaceConverter alternateConverter = null;
            var dictionary = pdfObject.Dictionary;
            componentCount = dictionary.GetIntegerOrDefault(PdfTokens.NKey);
            var alternateValue = ResolveByValue(dictionary.GetValue(PdfTokens.AlternateKey), componentCount);

            byte[] iccProfileBytes = null;
            if (pdfObject.HasStream)
            {
                var iccData = pdfObject.DecodeAsMemory();
                iccProfileBytes = iccData.ToArray();
            }

            return new IccBasedConverter(componentCount, alternateConverter, iccProfileBytes);
        }

        private PdfColorSpaceConverter CreatePatternColorSpace(IPdfValue colorSpaceValue)
        {
            if (colorSpaceValue != null && colorSpaceValue.Type == PdfValueType.Array)
            {
                var colorSpaceArray = colorSpaceValue.AsArray();
                if (colorSpaceArray != null && colorSpaceArray.Count >= 2)
                {
                    var baseColorSpaceValue = colorSpaceArray.GetValue(1);
                    var baseConverter = ResolveByValue(baseColorSpaceValue);
                    return new PatternColorSpaceConverter(baseConverter);
                }
            }
            return new PatternColorSpaceConverter(null);
        }

        private static PdfColorSpaceConverter CreateLabColorSpace(IPdfValue colorSpaceValue)
        {
            var dictionary = GetDictionaryValue(colorSpaceValue);
            if (dictionary == null)
            {
                return null;
            }

            var whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
            var range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();


            return new LabColorSpaceConverter(whitePoint, range);
        }

        private PdfFunction ResolveTintFunction(PdfArray colorSpaceArray, int tintFunctionIndex)
        {
            if (colorSpaceArray == null)
            {
                return null;
            }

            var tintFunctionObject = colorSpaceArray.GetPageObject(tintFunctionIndex);
            if (tintFunctionObject != null)
            {
                return PdfFunctions.GetFunction(tintFunctionObject);
            }

            return null;
        }

        private static PdfDictionary GetDictionaryValue(IPdfValue value)
        {
            if (value == null)
            {
                return null;
            }

            var parametersArray = value.AsArray();
            if (parametersArray == null || parametersArray.Count < 2)
            {
                return null;
            }

            return parametersArray.GetDictionary(1);
        }

        private static PdfObject GetObjectValue(IPdfValue value)
        {
            if (value == null)
            {
                return null;
            }

            var parametersArray = value.AsArray();
            if (parametersArray == null || parametersArray.Count < 2)
            {
                return null;
            }

            return parametersArray.GetPageObject(1);
        }
    }
}
