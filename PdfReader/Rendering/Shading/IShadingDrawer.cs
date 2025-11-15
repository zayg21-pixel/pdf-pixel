using SkiaSharp;
using PdfReader.Models;
using PdfReader.Shading.Model;
using PdfReader.Rendering.State;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Interface for shading drawing implementations (operator 'sh').
    /// Implementations receive a parsed <see cref="PdfShading"/> model rather than the raw dictionary.
    /// </summary>
    public interface IShadingDrawer
    {
        /// <summary>
        /// Draw a shading fill described by a parsed shading model.
        /// </summary>
        /// <param name="canvas">Canvas to draw on.</param>
        /// <param name="shading">Parsed shading model.</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">PDF page context (for resources, color spaces, soft masks, etc.).</param>
        void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page);
    }
}
