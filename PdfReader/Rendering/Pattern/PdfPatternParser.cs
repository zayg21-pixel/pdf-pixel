using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Pattern
{
    internal static class PdfPatternParser
    {
        /// <summary>
        /// Parse a pattern object (tiling or shading) and return a concrete PdfPattern instance.
        /// Returns null if unsupported or malformed.
        /// </summary>
        public static PdfPattern TryParsePattern(PdfObject patternObject, PdfPage page)
        {
            if (patternObject.Reference.IsValid)
            {
                if (page.Document.PatternCache.TryGetValue(patternObject.Reference, out var cachedPattern))
                {
                    return cachedPattern;
                }
            }

            var result = ParsePatternInternal(patternObject, page);

            if (result != null && patternObject.Reference.IsValid)
            {
                page.Document.PatternCache[patternObject.Reference] = result;
            }

            return result;
        }

        private static PdfPattern ParsePatternInternal(PdfObject patternObject, PdfPage page)
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

            var bbox = new SKRect(bboxArray[0], bboxArray[1], bboxArray[2], bboxArray[3]);

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

        private static PdfShadingPattern ParseShadingPattern(PdfObject patternObject, PdfPage page) // TODO: get rid of page arg
        {
            SKMatrix matrix = SKMatrix.Identity;
            var dictionary = patternObject.Dictionary;

            var matrixArray = dictionary.GetArray(PdfTokens.MatrixKey);
            if (matrixArray != null && matrixArray.Count >= 6)
            {
                matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
            }

            var shadingObject = dictionary.GetPageObject(PdfTokens.ShadingKey);

            if (shadingObject == null)
            {
                return null; // Invalid shading pattern without /Shading
            }

            PdfDictionary extGState = dictionary.GetDictionary(PdfTokens.ExtGStateKey);
            var shading = new PdfShading(shadingObject, page);

            return new PdfShadingPattern(page, patternObject, shading, matrix, extGState);
        }
    }
}
