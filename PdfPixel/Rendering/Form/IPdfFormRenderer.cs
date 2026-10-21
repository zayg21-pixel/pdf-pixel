using PdfPixel.Commands;
using PdfPixel.Forms;

namespace PdfPixel.Rendering.Form;

/// <summary>
/// Defines the contract for rendering PDF Form XObjects.
/// Implementations are responsible for drawing form content using the provided graphics state and tracking recursion.
/// </summary>
public interface IPdfFormRenderer
{
    /// <summary>
    /// Draws a PDF Form XObject using the given graphics state.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="formXObject">The PDF Form XObject to render.</param>
    /// <param name="graphicsState">The current graphics state for rendering.</param>
    void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState);
}
