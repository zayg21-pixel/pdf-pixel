using System;
using PdfPixel.Functions;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Color space resolver factory methods.
/// </summary>
internal partial class PdfColorSpaceResolver
{
    private PdfIndexedColorSpaceConverter? CreateIndexedColorSpace(IPdfValue colorSpaceValue)
    {
        if (colorSpaceValue == null)
        {
            return null;
        }

        PdfArray? colorSpaceArray = colorSpaceValue.AsArray();
        if (colorSpaceArray == null || colorSpaceArray.Count < 4)
        {
            return null;
        }

        PdfObject? baseColorSpaceValue = colorSpaceArray.GetObject(1);
        PdfColorSpaceConverter? baseConverter = ResolveByObject(baseColorSpaceValue);
        if (baseConverter == null)
        {
            return null;
        }

        int highestIndex = colorSpaceArray.GetIntegerOrDefault(2);
        if (highestIndex < 0)
        {
            highestIndex = 0;
        }

        byte[] lookupTableBytes = Array.Empty<byte>();

        PdfObject? lookupObject = colorSpaceArray.GetObject(3);
        if (lookupObject != null)
        {
            ReadOnlyMemory<byte> lookupData = lookupObject.DecodeAsMemory();
            if (!lookupData.IsEmpty)
            {
                lookupTableBytes = lookupData.ToArray();
            }
            else
            {
                PdfString? lookupString = lookupObject.Value.AsString();

                if (lookupString != null)
                {
                    lookupTableBytes = lookupString.Value.Value.ToArray();
                }
            }
        }

        return new PdfIndexedColorSpaceConverter(baseConverter, highestIndex, lookupTableBytes);
    }

    private PdfSeparationColorSpaceConverter? CreateSeparationColorSpace(IPdfValue colorSpaceValue)
    {
        if (colorSpaceValue == null)
        {
            return null;
        }

        PdfArray? colorSpaceArray = colorSpaceValue.AsArray();
        if (colorSpaceArray == null || colorSpaceArray.Count < 4)
        {
            return null;
        }

        PdfString? colorantName = colorSpaceArray.GetName(1);
        PdfColorSpaceConverter? alternateConverter = ResolveByValue(colorSpaceArray.GetValue(2));
        PdfFunction? tintFunction = ResolveTintFunction(colorSpaceArray, 3);
        return new PdfSeparationColorSpaceConverter(colorantName, alternateConverter, tintFunction);
    }

    private PdfDeviceNColorSpaceConverter? CreateDeviceNColorSpace(IPdfValue colorSpaceValue)
    {
        if (colorSpaceValue == null)
        {
            return null;
        }

        PdfArray? colorSpaceArray = colorSpaceValue.AsArray();
        if (colorSpaceArray == null || colorSpaceArray.Count < 4)
        {
            return null;
        }

        PdfArray? namesArray = colorSpaceArray.GetArray(1);
        if (namesArray == null || namesArray.Count == 0)
        {
            return null;
        }

        var colorantNames = new PdfString?[namesArray.Count];
        for (int i = 0; i < namesArray.Count; i++)
        {
            colorantNames[i] = namesArray.GetString(i);
        }

        PdfColorSpaceConverter? alternateConverter = ResolveByValue(colorSpaceArray.GetValue(2));
        PdfFunction? tintFunction = ResolveTintFunction(colorSpaceArray, 3);
        return new PdfDeviceNColorSpaceConverter(colorantNames, alternateConverter, tintFunction);
    }

    private PdfCalGrayColorSpaceConverter? CreateCalGrayColorSpace(IPdfValue colorSpaceValue)
    {
        PdfDictionary? dictionary = GetDictionaryValue(colorSpaceValue);
        if (dictionary == null)
        {
            return null;
        }


        float[]? whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
        float[]? blackPoint = dictionary.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
        float? gamma = dictionary.GetFloat(PdfTokens.GammaKey);

        return new PdfCalGrayColorSpaceConverter(whitePoint, blackPoint, gamma);
    }

    private PdfCalRgbColorSpaceConverter? CreateCalRrgbColorSpace(IPdfValue colorSpaceValue)
    {
        PdfDictionary? dictionary = GetDictionaryValue(colorSpaceValue);
        if (dictionary == null)
        {
            return null;
        }

        float[]? whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();

        float[]? gamma;
        float? gammaSingle = dictionary.GetFloat(PdfTokens.GammaKey);

        if (gammaSingle.HasValue)
        {
            gamma = new float[] { gammaSingle.Value, gammaSingle.Value, gammaSingle.Value };
        }
        else
        {
            float[]? gammaArray = dictionary.GetArray(PdfTokens.GammaKey)?.GetFloatArray();
            gamma = gammaArray;
        }

        float[]? blackPoint = dictionary.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();

        var matrix = new float[3, 3]
        {
            { 1f, 0f, 0f },
            { 0f, 1f, 0f },
            { 0f, 0f, 1f }
        };

        float[]? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey)?.GetFloatArray();
        if (matrixArray?.Length >= 9)
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

        return new PdfCalRgbColorSpaceConverter(whitePoint, blackPoint, gamma, matrix);
    }

    private PdfIccColorSpaceConverter? CreateIccColorSpace(IPdfValue colorSpaceValue)
    {
        PdfObject? pdfObject = GetObjectValue(colorSpaceValue);

        if (pdfObject == null)
        {
            return null;
        }

        PdfDictionary dictionary = pdfObject.Dictionary;
        int componentCount = dictionary.GetIntegerOrDefault(PdfTokens.NKey);
        PdfColorSpaceConverter? alternateConverter = ResolveByObject(dictionary.GetObject(PdfTokens.AlternateKey), componentCount);

        byte[]? iccProfileBytes = null;
        if (pdfObject.HasStream)
        {
            ReadOnlyMemory<byte> iccData = pdfObject.DecodeAsMemory();
            iccProfileBytes = iccData.ToArray();
        }

        return new PdfIccColorSpaceConverter(componentCount, alternateConverter, iccProfileBytes);
    }

    private PdfPatternColorSpaceConverter CreatePatternColorSpace(IPdfValue colorSpaceValue)
    {
        if (colorSpaceValue != null && colorSpaceValue.Type == PdfValueType.Array)
        {
            PdfArray? colorSpaceArray = colorSpaceValue.AsArray();
            if (colorSpaceArray?.Count >= 2)
            {
                PdfObject? baseColorSpaceValue = colorSpaceArray.GetObject(1);
                PdfColorSpaceConverter? baseConverter = ResolveByObject(baseColorSpaceValue);
                return new PdfPatternColorSpaceConverter(baseConverter);
            }
        }

        return new PdfPatternColorSpaceConverter(null);
    }

    private static PdfLabColorSpaceConverter? CreateLabColorSpace(IPdfValue colorSpaceValue)
    {
        PdfDictionary? dictionary = GetDictionaryValue(colorSpaceValue);
        if (dictionary == null)
        {
            return null;
        }

        float[]? whitePoint = dictionary.GetArray(PdfTokens.WhitePointKey)?.GetFloatArray();
        float[]? blackPoint = dictionary.GetArray(PdfTokens.BlackPointKey)?.GetFloatArray();
        float[]? range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        return new PdfLabColorSpaceConverter(whitePoint, blackPoint, range);
    }

    private PdfFunction? ResolveTintFunction(PdfArray colorSpaceArray, int tintFunctionIndex)
    {
        if (colorSpaceArray == null)
        {
            return null;
        }

        PdfObject? tintFunctionObject = colorSpaceArray.GetObject(tintFunctionIndex);
        if (tintFunctionObject != null)
        {
            return PdfFunctions.GetFunction(tintFunctionObject);
        }

        return null;
    }

    private static PdfDictionary? GetDictionaryValue(IPdfValue value)
    {
        if (value == null)
        {
            return null;
        }

        PdfArray? parametersArray = value.AsArray();
        if (parametersArray == null || parametersArray.Count < 2)
        {
            return null;
        }

        return parametersArray.GetDictionary(1);
    }

    private static PdfObject? GetObjectValue(IPdfValue? value)
    {
        if (value == null)
        {
            return null;
        }

        PdfArray? parametersArray = value.AsArray();
        if (parametersArray == null || parametersArray.Count < 2)
        {
            return null;
        }

        return parametersArray.GetObject(1);
    }
}
