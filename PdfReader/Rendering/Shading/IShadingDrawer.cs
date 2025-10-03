using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Interface for shading drawing implementations (operator 'sh').
    /// </summary>
    public interface IShadingDrawer
    {
        /// <summary>
        /// Draw a shading fill described by a shading dictionary.
        /// </summary>
        /// <param name="canvas">Canvas to draw on</param>
        /// <param name="shading">Shading dictionary</param>
        /// <param name="state">Current graphics state</param>
        /// <param name="page">PDF page context (for soft mask, resources, etc.)</param>
        void DrawShading(SKCanvas canvas, PdfDictionary shading, PdfGraphicsState state, PdfPage page);
    }
}
