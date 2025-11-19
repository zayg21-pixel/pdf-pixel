using PdfReader.Forms;
using PdfReader.Rendering.State;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering.Form;

/// <summary>
/// Defines the contract for rendering PDF Form XObjects onto a SkiaSharp canvas.
/// Implementations are responsible for drawing form content using the provided graphics state and tracking recursion.
/// </summary>
public interface IFormRenderer
{
    /// <summary>
    /// Draws a PDF Form XObject onto the specified canvas using the given graphics state.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="formXObject">The PDF Form XObject to render.</param>
    /// <param name="graphicsState">The current graphics state for rendering.</param>
    /// <param name="processingXObjects">A set of XObject references currently being processed to prevent recursion.</param>
    void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState, HashSet<uint> processingXObjects);
}
