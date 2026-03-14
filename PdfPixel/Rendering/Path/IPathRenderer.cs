using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Interface for path drawing implementations.
/// </summary>
public interface IPathRenderer
{
    /// <summary>
    /// Draw a path with the specified operation and fill type.
    /// </summary>
    /// <param name="processor">Command processor to draw through.</param>
    /// <param name="path">Path to draw.</param>
    /// <param name="state">Graphics state containing style information.</param>
    /// <param name="operation">Paint operation (stroke, fill, or both).</param>
    void DrawPath(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PaintOperation operation);
}