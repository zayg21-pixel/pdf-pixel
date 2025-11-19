using PdfReader.Color.Paint;
using PdfReader.Fonts.Types;
using PdfReader.Forms;
using PdfReader.Imaging.Model;
using PdfReader.Rendering.State;
using PdfReader.Shading.Model;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Defines the contract for PDF rendering operations, including drawing forms, images, paths, shadings, and text sequences.
    /// Implementations are responsible for rendering PDF content to a SkiaSharp canvas using the provided graphics state and resources.
    /// </summary>
    public interface IPdfRenderer
    {
        /// <summary>
        /// Draws a PDF Form XObject onto the specified canvas using the given graphics state.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="formXObject">The PDF Form XObject to render.</param>
        /// <param name="graphicsState">The current graphics state for rendering.</param>
        /// <param name="processingXObjects">A set of XObject references currently being processed to prevent recursion.</param>
        void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState, HashSet<uint> processingXObjects);

        /// <summary>
        /// Draws a PDF image onto the specified canvas using the given graphics state.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="pdfImage">The PDF image to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state);

        /// <summary>
        /// Draws a path onto the specified canvas using the given graphics state, paint operation, and fill type.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="path">The path to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        /// <param name="operation">The paint operation (stroke, fill, etc.).</param>
        /// <param name="fillType">The fill type for the path.</param>
        void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, SKPathFillType fillType);

        /// <summary>
        /// Draws a shading pattern onto the specified canvas using the given graphics state.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="shading">The PDF shading to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state);

        /// <summary>
        /// Draws a sequence of PDF text fragments and positioning adjustments onto the specified canvas using the given graphics state and font.
        /// </summary>
        /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
        /// <param name="sequence">The text sequence to render.</param>
        /// <param name="state">The current graphics state for rendering.</param>
        /// <param name="font">The font to use for rendering the text.</param>
        /// <returns>The total width of the rendered text sequence.</returns>
        float DrawTextSequence(SKCanvas canvas, PdfTextSequence sequence, PdfGraphicsState state, PdfFontBase font);
    }
}