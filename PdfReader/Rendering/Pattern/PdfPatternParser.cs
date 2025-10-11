using System;
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
        public static PdfPattern TryParsePattern(PdfReference reference, PdfObject obj)
        {
            try
            {
                var dict = obj.Dictionary;
                if (dict == null)
                {
                    return null;
                }

                int patternType = dict.GetIntegerOrDefault(PdfTokens.PatternTypeKey);
                switch (patternType)
                {
                    case 1:
                        return ParseTilingPattern(reference, dict);
                    case 2:
                        return ParseShadingPattern(reference, dict);
                    default:
                        return null; // Unsupported pattern type
                }
            }
            catch
            {
                // Swallow and return null for malformed pattern objects.
                return null;
            }
        }

        private static PdfTilingPattern ParseTilingPattern(PdfReference reference, PdfDictionary dict)
        {
            var bboxArray = dict.GetArray(PdfTokens.BBoxKey);
            if (bboxArray == null || bboxArray.Count < 4)
            {
                return null;
            }

            var bbox = new SKRect(
                bboxArray.GetFloat(0),
                bboxArray.GetFloat(1),
                bboxArray.GetFloat(2),
                bboxArray.GetFloat(3));

            float xStep = dict.GetFloatOrDefault(PdfTokens.XStepKey);
            float yStep = dict.GetFloatOrDefault(PdfTokens.YStepKey);
            int rawPaintType = dict.GetIntegerOrDefault(PdfTokens.PaintTypeKey);
            int rawTilingType = dict.GetIntegerOrDefault(PdfTokens.TilingTypeKey);

            PdfTilingPaintType paintTypeKind = rawPaintType == 2 ? PdfTilingPaintType.Uncolored : PdfTilingPaintType.Colored;
            PdfTilingSpacingType tilingTypeKind = rawTilingType switch
            {
                2 => PdfTilingSpacingType.NoDistortion,
                3 => PdfTilingSpacingType.ConstantSpacingFast,
                _ => PdfTilingSpacingType.ConstantSpacing
            };

            SKMatrix matrix = SKMatrix.Identity;
            var matrixArray = dict.GetArray(PdfTokens.MatrixKey);
            if (matrixArray != null && matrixArray.Count >= 6)
            {
                matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
            }

            return new PdfTilingPattern(
                reference,
                bbox,
                xStep,
                yStep,
                paintTypeKind,
                tilingTypeKind,
                matrix);
        }

        private static PdfShadingPattern ParseShadingPattern(PdfReference reference, PdfDictionary dict)
        {
            SKMatrix matrix = SKMatrix.Identity;
            var matrixArray = dict.GetArray(PdfTokens.MatrixKey);
            if (matrixArray != null && matrixArray.Count >= 6)
            {
                matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
            }

            PdfDictionary shadingObject = dict.GetDictionary(PdfTokens.ShadingKey);
            if (shadingObject == null)
            {
                return null; // Invalid shading pattern without /Shading
            }

            PdfDictionary extGState = dict.GetDictionary(PdfTokens.ExtGStateKey);
            var shading = new PdfShading(shadingObject);

            return new PdfShadingPattern(reference, shading, matrix, extGState);
        }
    }
}
