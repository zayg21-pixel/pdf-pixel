using SkiaSharp;
using PdfReader.Models;
using PdfReader.Shading.Model;
using PdfReader.Pattern.Model;
using PdfReader.Rendering.Operators;
using PdfReader.Text;
using PdfReader.Rendering;

namespace PdfReader.Pattern.Utilities;

internal static class PdfPatternParser
{
    /// <summary>
    /// Parses a PDF pattern object and returns the corresponding <see cref="PdfPattern"/> instance.
    /// </summary>
    /// <remarks>This method supports parsing tiling patterns (PatternType 1) and shading patterns
    /// (PatternType 2).  Patterns with other types are not supported and will result in a <see langword="null"/>
    /// return value.</remarks>
    /// <param name="renderer">The PDF renderer instance used for context during parsing.</param>
    /// <param name="patternObject">The PDF object representing the pattern. Must contain a valid dictionary with a <c>PatternType</c> key.</param>
    /// <returns>A <see cref="PdfPattern"/> instance representing the parsed pattern, or <see langword="null"/> if the
    /// pattern type is unsupported.</returns>
    public static PdfPattern ParsePattern(IPdfRenderer renderer, PdfObject patternObject)
    {
        int patternType = patternObject.Dictionary.GetIntegerOrDefault(PdfTokens.PatternTypeKey);
        return patternType switch
        {
            1 => ParseTilingPattern(renderer, patternObject),
            2 => ParseShadingPattern(patternObject),
            _ => null,// Unsupported pattern type
        };
    }

    private static PdfTilingPattern ParseTilingPattern(IPdfRenderer renderer, PdfObject patternObject)
    {
        var dictionary = patternObject.Dictionary;

        var bboxArray = dictionary.GetArray(PdfTokens.BBoxKey);
        var bbox = PdfLocationUtilities.CreateBBox(bboxArray) ?? SKRect.Empty;

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

        var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        SKMatrix matrix = PdfLocationUtilities.CreateMatrix(matrixArray) ?? SKMatrix.Identity;

        return new PdfTilingPattern(
            renderer,
            patternObject,
            bbox,
            xStep,
            yStep,
            paintTypeKind,
            tilingTypeKind,
            matrix);
    }

    private static PdfShadingPattern ParseShadingPattern(PdfObject patternObject)
    {
        var dictionary = patternObject.Dictionary;

        var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        var matrix = PdfLocationUtilities.CreateMatrix(matrixArray) ?? SKMatrix.Identity;

        var shadingObject = dictionary.GetObject(PdfTokens.ShadingKey);

        if (shadingObject == null)
        {
            return null; // Invalid shading pattern without /Shading
        }

        PdfDictionary extGState = dictionary.GetDictionary(PdfTokens.ExtGStateKey);
        var shading = new PdfShading(shadingObject);

        return new PdfShadingPattern(patternObject, shading, matrix, extGState);
    }
}
