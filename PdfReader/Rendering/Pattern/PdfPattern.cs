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
    public abstract class PdfPattern : IDisposable
    {
        protected PdfPattern(PdfPage page, PdfObject sourceObject, SKMatrix matrix, PdfPatternType patternType)
        {
            Page = page;
            SourceObject = sourceObject;
            PatternMatrix = matrix;
            PatternType = patternType;
        }

        /// <summary>
        /// Owning page context for the pattern.
        /// </summary>
        public PdfPage Page { get; set; }

        /// <summary>Original source PDF object for the pattern.</summary>
        public PdfObject SourceObject { get; }

        /// <summary>Pattern transformation matrix (identity if /Matrix absent).</summary>
        public SKMatrix PatternMatrix { get; }

        /// <summary>Underlying pattern type enum.</summary>
        public PdfPatternType PatternType { get; }

        /// <summary>
        /// Converts the current object to reusable <see cref="SKShader"/> representation.
        /// </summary>
        /// <param name="intent">The rendering intent that specifies how colors should be managed during rendering.</param>
        /// <param name="state">The graphics state that provides additional context for rendering, such as transformations and clipping
        /// paths.</param>
        /// <returns>An <see cref="SKShader"/> instance representing the current object, configured based on the specified
        /// rendering intent and graphics state.</returns>
        public abstract SKShader AsShader(PdfRenderingIntent intent, PdfGraphicsState state);

        public virtual void Dispose()
        {
        }
    }
}
