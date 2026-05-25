using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Rendering
{
    /// <summary>
    /// Defines the contract for PDF rendering operations, including drawing forms, images, paths, shadings, and text sequences.
    /// Implementations are responsible for rendering PDF content via an <see cref="IPdfCommandProcessor"/>
    /// using the provided graphics state and resources.
    /// </summary>
    public interface IPdfRenderer
    {
        /// <summary>
        /// Draws a PDF Form XObject using the given graphics state.
        /// </summary>
        /// <param name="processor">The command processor to draw through.</param>
        /// <param name="formXObject">The PDF Form XObject to render.</param>
        /// <param name="graphicsState">The current graphics state for rendering.</param>
        void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState);

        /// <summary>
        /// Draws a PDF image using the given graphics state.
        /// </summary>
        /// <param name="processor">The command processor to draw through.</param>
        /// <param name="pdfImage">The PDF image to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state);

        /// <summary>
        /// Draws a path using the given graphics state, paint operation, and fill type.
        /// </summary>
        /// <param name="processor">The command processor to draw through.</param>
        /// <param name="path">The path to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        /// <param name="operation">The paint operation (stroke, fill, etc.).</param>
        void DrawPath(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PaintOperation operation);

        /// <summary>
        /// Draws a shading pattern using the given graphics state.
        /// </summary>
        /// <param name="processor">The command processor to draw through.</param>
        /// <param name="shading">The PDF shading to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        void DrawShading(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state);

        /// <summary>
        /// Draws a sequence of PDF text fragments and positioning adjustments using the given graphics state and font.
        /// </summary>
        /// <param name="processor">The command processor to draw through.</param>
        /// <param name="glyphs">Collection of glyphs to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        /// <param name="font">The font to use for rendering the text.</param>
        /// <returns>The total advancement of the text sequence.</returns>
        SKSize DrawTextSequence(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font);
    }
}
