using System;
using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Pattern
{
    public enum PdfPatternType
    {
        Tiling = 1,
        Shading = 2
    }

    /// <summary>
    /// Base class for all PDF pattern types (/Pattern). Provides common metadata shared by
    /// tiling and shading patterns.
    /// </summary>
    public abstract class PdfPattern
    {
        /// <summary>Pattern object reference.</summary>
        public PdfReference Reference { get; }
        /// <summary>Pattern transformation matrix (identity if /Matrix absent).</summary>
        public SKMatrix PatternMatrix { get; }
        /// <summary>Underlying pattern type enum.</summary>
        public PdfPatternType PatternType { get; }

        protected PdfPattern(PdfReference reference, SKMatrix matrix, PdfPatternType patternType)
        {
            Reference = reference;
            PatternMatrix = matrix;
            PatternType = patternType;
        }
    }

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
        public SKRect BBox { get; }
        public float XStep { get; }
        public float YStep { get; }
        public PdfTilingPaintType PaintTypeKind { get; }
        public PdfTilingSpacingType TilingTypeKind { get; }

        internal PdfTilingPattern(
            PdfReference reference,
            SKRect bbox,
            float xStep,
            float yStep,
            PdfTilingPaintType paintTypeKind,
            PdfTilingSpacingType tilingTypeKind,
            SKMatrix matrix)
            : base(reference, matrix, PdfPatternType.Tiling)
        {
            BBox = bbox;
            XStep = xStep;
            YStep = yStep;
            PaintTypeKind = paintTypeKind;
            TilingTypeKind = tilingTypeKind;
        }
    }

    /// <summary>
    /// Represents a shading pattern (/PatternType 2). Minimal placeholder – only captures core references.
    /// </summary>
    public sealed class PdfShadingPattern : PdfPattern
    {
        /// <summary>Shading object referenced by the pattern's /Shading entry.</summary>
        public PdfShading Shading { get; }
        /// <summary>Optional ExtGState dictionary (may be null).</summary>
        public PdfDictionary ExtGState { get; }

        internal PdfShadingPattern(
            PdfReference reference,
            PdfShading shading,
            SKMatrix matrix,
            PdfDictionary extGState)
            : base(reference, matrix, PdfPatternType.Shading)
        {
            Shading = shading;
            ExtGState = extGState;
        }
    }
}
