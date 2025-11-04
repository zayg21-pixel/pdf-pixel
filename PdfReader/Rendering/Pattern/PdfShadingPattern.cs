using PdfReader.Models;
using PdfReader.Rendering.Shading;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Pattern
{
    /// <summary>
    /// Represents a shading pattern (/PatternType 2) in a PDF document.
    /// Provides access to the referenced shading and optional extended graphics state.
    /// Caches the base shader for performance.
    /// </summary>
    public sealed class PdfShadingPattern : PdfPattern
    {
        private SKShader _cachedBaseShader;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfShadingPattern"/> class with the specified parameters.
        /// </summary>
        /// <param name="page">The owning PDF page context.</param>
        /// <param name="sourceObject">The original source PDF object for the pattern.</param>
        /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
        /// <param name="matrix">The pattern transformation matrix.</param>
        /// <param name="extGState">Optional extended graphics state dictionary (may be null).</param>
        internal PdfShadingPattern(
            PdfPage page,
            PdfObject sourceObject,
            PdfShading shading,
            SKMatrix matrix,
            PdfDictionary extGState)
            : base(page, sourceObject, matrix, PdfPatternType.Shading)
        {
            Shading = shading;
            ExtGState = extGState;
        }

        /// <summary>
        /// Gets the shading object referenced by the pattern's /Shading entry.
        /// </summary>
        public PdfShading Shading { get; }

        /// <summary>
        /// Gets the optional extended graphics state dictionary (may be null).
        /// </summary>
        public PdfDictionary ExtGState { get; } // TODO: use

        /// <summary>
        /// Returns an <see cref="SKShader"/> for this shading pattern, replicating the logic of <see cref="PatternPaintEngine.ToShader"/>.
        /// Caches the base shader for reuse and performance.
        /// </summary>
        /// <param name="intent">The rendering intent for color conversion.</param>
        /// <param name="state">The current graphics state.</param>
        /// <returns>An <see cref="SKShader"/> instance or null if creation fails.</returns>
        public override SKShader AsShader(PdfRenderingIntent intent, PdfGraphicsState state)
        {
            if (_cachedBaseShader == null)
            {
                _cachedBaseShader = PdfShadingBuilder.ToShader(Shading);
            }

            if (_cachedBaseShader == null)
            {
                return null;
            }

            SKMatrix localMatrix = SKMatrix.Concat(state.CTM.Invert(), PatternMatrix);

            return _cachedBaseShader.WithLocalMatrix(localMatrix);
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
