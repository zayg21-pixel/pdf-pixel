using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Shading;

namespace PdfPixel.Pattern;

internal static class PdfPatternParser
{
    /// <summary>
    /// Parses a PDF pattern object and returns the corresponding <see cref="PdfPattern"/> instance.
    /// </summary>
    /// <remarks>This method supports parsing tiling patterns (PatternType 1) and shading patterns
    /// (PatternType 2).  Patterns with other types are not supported and will result in a <see langword="null"/>
    /// return value.</remarks>
    /// <param name="patternObject">The PDF object representing the pattern. Must contain a valid dictionary with a <c>PatternType</c> key.</param>
    /// <returns>A <see cref="PdfPattern"/> instance representing the parsed pattern, or <see langword="null"/> if the
    /// pattern type is unsupported.</returns>
    public static PdfPattern? ParsePattern(PdfObject patternObject)
    {
        int patternType = patternObject.Dictionary.GetIntegerOrDefault(PdfTokens.PatternTypeKey);
        return patternType switch
        {
            1 => ParseTilingPattern(patternObject),
            2 => ParseShadingPattern(patternObject),
            _ => null// Unsupported pattern type
        };
    }

    private static PdfTilingPattern? ParseTilingPattern(PdfObject patternObject)
    {
        if (patternObject.Reference == null)
        {
            return null;
        }

        PdfDictionary dictionary = patternObject.Dictionary;

        PdfArray? boundingBoxArray = dictionary.GetArray(PdfTokens.BBoxKey);
        PdfRectangle boundingBox = PdfRectangle.FromArray(boundingBoxArray) ?? PdfRectangle.Empty;

        float xStep = dictionary.GetFloatOrDefault(PdfTokens.XStepKey);
        float yStep = dictionary.GetFloatOrDefault(PdfTokens.YStepKey);
        int? rawPaintType = dictionary.GetInteger(PdfTokens.PaintTypeKey);
        int? rawTilingType = dictionary.GetInteger(PdfTokens.TilingTypeKey);

        PdfTilingPaintType? paintType = rawPaintType switch
        {
            1 => PdfTilingPaintType.Colored,
            2 => PdfTilingPaintType.Uncolored,
            _ => null
        };
        PdfTilingType? tilingType = rawTilingType switch
        {
            1 => PdfTilingType.ConstantSpacing,
            2 => PdfTilingType.NoDistortion,
            3 => PdfTilingType.ConstantSpacingFast,
            _ => null
        };

        PdfArray? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;

        return new PdfTilingPattern(
            patternObject.Reference.Value,
            patternObject.Stream,
            dictionary.GetDictionary(PdfTokens.ResourcesKey),
            boundingBox,
            xStep,
            yStep,
            paintType,
            tilingType,
            matrix);
    }

    private static PdfShadingPattern? ParseShadingPattern(PdfObject patternObject)
    {
        PdfDictionary dictionary = patternObject.Dictionary;

        PdfArray? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;

        PdfShading? shading = PdfShading.GetShading(dictionary, PdfTokens.ShadingKey);

        if (shading == null)
        {
            return null; // Invalid shading pattern without /Shading
        }

        PdfGraphicsStateParameters? extGState = PdfGraphicsStateParameters.GetGraphicsStateParameters(dictionary, PdfTokens.ExtGStateKey);

        return new PdfShadingPattern(shading, matrix, extGState);
    }
}
