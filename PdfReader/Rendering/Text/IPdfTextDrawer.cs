using PdfReader.Fonts;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Defines the contract for drawing PDF text onto a SkiaSharp canvas.
    /// Implementations are responsible for rendering text using the provided font, graphics state, and page context.
    /// </summary>
    public interface IPdfTextDrawer
    {
        /// <summary>
        /// Draws a text string onto the specified canvas using the given font and graphics state.
        /// Returns the horizontal advancement (in user space units) after drawing the text.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="pdfText">The PDF text to render.</param>
        /// <param name="page">The current PDF page context.</param>
        /// <param name="state">The current graphics state.</param>
        /// <param name="font">The font to use for rendering.</param>
        /// <returns>The horizontal advancement after drawing the text.</returns>
        float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font);

        /// <summary>
        /// Draws a text array with positioning adjustments onto the specified canvas using the given font and graphics state.
        /// Returns the total horizontal advancement (in user space units) after drawing the text.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="arrayOperand">The PDF array operand containing text and positioning instructions.</param>
        /// <param name="page">The current PDF page context.</param>
        /// <param name="state">The current graphics state.</param>
        /// <param name="font">The font to use for rendering.</param>
        /// <returns>The total horizontal advancement after drawing the text array.</returns>
        float DrawTextWithPositioning(SKCanvas canvas, IPdfValue arrayOperand, PdfPage page, PdfGraphicsState state, PdfFontBase font);
    }
}