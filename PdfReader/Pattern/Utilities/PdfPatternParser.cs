using SkiaSharp;
using PdfReader.Models;
using PdfReader.Shading.Model;
using PdfReader.Pattern.Model;
using PdfReader.Rendering.Operators;
using PdfReader.Text;

namespace PdfReader.Pattern.Utilities;

internal static class PdfPatternParser
{
    /// <summary>
    /// Parses a PDF pattern object and returns the corresponding <see cref="PdfPattern"/> instance.
    /// </summary>
    /// <remarks>This method supports parsing tiling patterns (PatternType 1) and shading patterns
    /// (PatternType 2).  Patterns with other types are not supported and will result in a <see langword="null"/>
    /// return value.</remarks>
    /// <param name="patternObject">The PDF object representing the pattern. Must contain a valid dictionary with a <c>PatternType</c> key.</param>
    /// <param name="page">The <see cref="PdfPage"/> associated with the pattern, used for resolving resources.</param>
    /// <returns>A <see cref="PdfPattern"/> instance representing the parsed pattern, or <see langword="null"/> if the
    /// pattern type is unsupported.</returns>
    public static PdfPattern ParsePattern(PdfObject patternObject, PdfPage page)
    {
        int patternType = patternObject.Dictionary.GetIntegerOrDefault(PdfTokens.PatternTypeKey);
        return patternType switch
        {
            1 => ParseTilingPattern(patternObject, page),
            2 => ParseShadingPattern(patternObject, page),
            _ => null,// Unsupported pattern type
        };
    }

    private static PdfTilingPattern ParseTilingPattern(PdfObject patternObject, PdfPage page)
    {
        var dictionary = patternObject.Dictionary;

        var bboxArray = dictionary.GetArray(PdfTokens.BBoxKey).GetFloatArray();

        if (bboxArray.Length < 4)
        {
            return null;
        }

        var bbox = new SKRect(bboxArray[0], bboxArray[1], bboxArray[2], bboxArray[3]).Standardized;

        float xStep = dictionary.GetFloatOrDefault(PdfTokens.XStepKey);
        float yStep = dictionary.GetFloatOrDefault(PdfTokens.YStepKey);
        int rawPaintType = dictionary.GetIntegerOrDefault(PdfTokens.PaintTypeKey);
        int rawTilingType = dictionary.GetIntegerOrDefault(PdfTokens.TilingTypeKey);

        PdfTilingPaintType paintTypeKind = rawPaintType == 2 ? PdfTilingPaintType.Uncolored : PdfTilingPaintType.Colored;
        PdfTilingSpacingType tilingTypeKind = rawTilingType switch
        {
            2 => PdfTilingSpacingType.NoDistortion,
            3 => PdfTilingSpacingType.ConstantSpacingFast,
            _ => PdfTilingSpacingType.ConstantSpacing
        };

        SKMatrix matrix = SKMatrix.Identity;
        var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        if (matrixArray != null && matrixArray.Count >= 6)
        {
            matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
        }

        return new PdfTilingPattern(
            page,
            patternObject,
            bbox,
            xStep,
            yStep,
            paintTypeKind,
            tilingTypeKind,
            matrix);
    }

    private static PdfShadingPattern ParseShadingPattern(PdfObject patternObject, PdfPage page)
    {
        SKMatrix matrix = SKMatrix.Identity;
        var dictionary = patternObject.Dictionary;

        var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        if (matrixArray != null && matrixArray.Count >= 6)
        {
            matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
        }

        var shadingObject = dictionary.GetObject(PdfTokens.ShadingKey);

        if (shadingObject == null)
        {
            return null; // Invalid shading pattern without /Shading
        }

        PdfDictionary extGState = dictionary.GetDictionary(PdfTokens.ExtGStateKey);
        var shading = new PdfShading(shadingObject, page);

        return new PdfShadingPattern(page, patternObject, shading, matrix, extGState);
    }
}
