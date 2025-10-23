using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering.Pattern
{
    /// <summary>
    /// Strongly typed paint type for tiling patterns (PDF spec Table 90)
    /// </summary>
    public enum PdfTilingPaintType
    {
        Colored = 1,
        Uncolored = 2
    }

    /// <summary>
    /// Strongly typed tiling type (PDF spec Table 91)
    /// </summary>
    public enum PdfTilingSpacingType
    {
        ConstantSpacing = 1,
        NoDistortion = 2,
        ConstantSpacingFast = 3
    }

    /// <summary>
    /// Represents a parsed tiling (/PatternType 1) pattern.
    /// </summary>
    public sealed class PdfTilingPattern : PdfPattern
    {
        private SKShader _cachedBaseShader;

        internal PdfTilingPattern(
            PdfPage page,
            PdfObject sourceObject,
            SKRect bbox,
            float xStep,
            float yStep,
            PdfTilingPaintType paintTypeKind,
            PdfTilingSpacingType tilingTypeKind,
            SKMatrix matrix)
            : base(page, sourceObject, matrix, PdfPatternType.Tiling)
        {
            BBox = bbox;
            XStep = xStep;
            YStep = yStep;
            PaintTypeKind = paintTypeKind;
            TilingTypeKind = tilingTypeKind;
        }

        /// <summary>
        /// Gets the bounding box of the pattern cell.
        /// </summary>
        public SKRect BBox { get; }

        /// <summary>
        /// Gets the horizontal spacing between pattern cells.
        /// </summary>
        public float XStep { get; }

        /// <summary>
        /// Gets the vertical spacing between pattern cells.
        /// </summary>
        public float YStep { get; }

        /// <summary>
        /// Gets the paint type (colored or uncolored).
        /// </summary>
        public PdfTilingPaintType PaintTypeKind { get; }

        /// <summary>
        /// Gets the tiling type (spacing and distortion rules).
        /// </summary>
        public PdfTilingSpacingType TilingTypeKind { get; }

        /// <summary>
        /// Returns an <see cref="SKShader"/> for this tiling pattern.
        /// Caches the base shader for reuse and performance.
        /// </summary>
        /// <param name="intent">The rendering intent for color conversion.</param>
        /// <param name="state">The current graphics state.</param>
        /// <returns>An <see cref="SKShader"/> instance or null if creation fails.</returns>
        public override SKShader AsShader(PdfRenderingIntent intent, PdfGraphicsState state)
        {
            if (_cachedBaseShader == null)
            {
                _cachedBaseShader = TilingPatternShaderBuilder.ToBaseShader(this, Page);
            }

            if (_cachedBaseShader == null)
            {
                return null;
            }

            SKMatrix ctmInverse;
            bool haveCtmInverse = state.CTM.TryInvert(out ctmInverse);
            SKMatrix localMatrix = haveCtmInverse
                ? SKMatrix.Concat(ctmInverse, PatternMatrix)
                : PatternMatrix;

            var transformedShader = _cachedBaseShader.WithLocalMatrix(localMatrix);

            if (PaintTypeKind == PdfTilingPaintType.Uncolored)
            {
                if (state.FillPaint != null && state.FillPaint.PatternComponents != null)
                {
                    var patternColorSpace = state.FillColorConverter as PatternColorSpaceConverter;
                    if (patternColorSpace != null && patternColorSpace.BaseColorSpace != null)
                    {
                        SKColor tintColor = patternColorSpace.BaseColorSpace.ToSrgb(state.FillPaint.PatternComponents, state.RenderingIntent);
                        var tintColorFilter = SKColorFilter.CreateBlendMode(tintColor, SKBlendMode.SrcIn);
                        return transformedShader.WithColorFilter(tintColorFilter);
                    }
                }
            }

            return transformedShader;
        }

        /// <summary>
        /// Disposes the cached shader and releases resources.
        /// </summary>
        public override void Dispose()
        {
            if (_cachedBaseShader != null)
            {
                _cachedBaseShader.Dispose();
                _cachedBaseShader = null;
            }

            base.Dispose();
        }
    }
}
