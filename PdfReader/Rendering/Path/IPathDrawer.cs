using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Path
{
    /// <summary>
    /// Interface for path drawing implementations
    /// </summary>
    public interface IPathDrawer
    {
        /// <summary>
        /// Draw a path with the specified operation and fill type
        /// </summary>
        /// <param name="canvas">Canvas to draw on</param>
        /// <param name="path">Path to draw</param>
        /// <param name="state">Graphics state containing style information</param>
        /// <param name="operation">Paint operation (stroke, fill, or both)</param>
        /// <param name="page">PDF page context for soft mask processing (optional)</param>
        /// <param name="fillType">Fill type for the path</param>
        void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, PdfPage page, SKPathFillType fillType);
    }
}