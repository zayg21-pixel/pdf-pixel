using PdfPixel.Commands.Model;
using PdfPixel.Forms;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Form;

/// <summary>
/// Defines the contract for rendering PDF Form XObjects.
/// Implementations are responsible for drawing form content using the provided graphics state and tracking recursion.
/// </summary>
public interface IFormRenderer
{
    /// <summary>
    /// Draws a PDF Form XObject using the given graphics state.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="formXObject">The PDF Form XObject to render.</param>
    /// <param name="graphicsState">The current graphics state for rendering.</param>
    void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState);
}
