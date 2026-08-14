using PdfPixel.Models;
using PdfPixel.Shading.Model;
using PdfPixel.Pattern.Model;
using PdfPixel.Text;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Geometry;

namespace PdfPixel.Pattern.Utilities;

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
    /// <param name="page">The page owning the pattern, used to parse the shading pattern's graphics state parameters.</param>
    /// <returns>A <see cref="PdfPattern"/> instance representing the parsed pattern, or <see langword="null"/> if the
    /// pattern type is unsupported.</returns>
    public static PdfPattern? ParsePattern(IPdfRenderer renderer, PdfObject patternObject, IPdfPageInternal page)
    {
        int patternType = patternObject.Dictionary.GetIntegerOrDefault(PdfTokens.PatternTypeKey);
        return patternType switch
        {
            1 => ParseTilingPattern(renderer, patternObject),
            2 => ParseShadingPattern(patternObject, page),
            _ => null// Unsupported pattern type
        };
    }

    private static PdfTilingPattern ParseTilingPattern(IPdfRenderer renderer, PdfObject patternObject)
    {
        PdfDictionary dictionary = patternObject.Dictionary;

        PdfArray? bboxArray = dictionary.GetArray(PdfTokens.BBoxKey);
        PdfRectangle bbox = PdfRectangle.FromArray(bboxArray) ?? PdfRectangle.Empty;

        float xStep = dictionary.GetFloatOrDefault(PdfTokens.XStepKey);
        float yStep = dictionary.GetFloatOrDefault(PdfTokens.YStepKey);
        int rawPaintType = dictionary.GetIntegerOrDefault(PdfTokens.PaintTypeKey);
        int rawTilingType = dictionary.GetIntegerOrDefault(PdfTokens.TilingTypeKey);

        PdfTilingPaintType paintTypeKind = (rawPaintType == 2) ? PdfTilingPaintType.Uncolored : PdfTilingPaintType.Colored;
        PdfTilingSpacingType tilingTypeKind = rawTilingType switch
        {
            2 => PdfTilingSpacingType.NoDistortion,
            3 => PdfTilingSpacingType.ConstantSpacingFast,
            _ => PdfTilingSpacingType.ConstantSpacing
        };

        PdfArray? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;

        return new PdfTilingPattern(
            renderer,
            patternObject.Reference,
            patternObject.Stream,
            dictionary.GetDictionary(PdfTokens.ResourcesKey),
            bbox,
            xStep,
            yStep,
            paintTypeKind,
            tilingTypeKind,
            matrix);
    }

    private static PdfShadingPattern? ParseShadingPattern(PdfObject patternObject, IPdfPageInternal page)
    {
        PdfDictionary dictionary = patternObject.Dictionary;

        PdfArray? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;

        PdfObject? shadingObject = dictionary.GetObject(PdfTokens.ShadingKey);

        if (shadingObject == null)
        {
            return null; // Invalid shading pattern without /Shading
        }

        PdfDictionary? extGStateDictionary = dictionary.GetDictionary(PdfTokens.ExtGStateKey);
        PdfGraphicsStateParameters? extGState = (extGStateDictionary == null)
            ? null
            : PdfGraphicsStateParser.ParseGraphicsStateParametersFromDictionary(extGStateDictionary, page);

        PdfShading shading = PdfShading.GetShading(shadingObject);

        return new PdfShadingPattern(shading, matrix, extGState);
    }
}
