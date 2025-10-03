using PdfReader.Fonts;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Interface for text drawing implementations
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public interface ITextDrawer
    {
        /// <summary>
        /// Draw text and return the advancement width
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        /// <param name="canvas">Canvas to draw on</param>
        /// <param name="pdfText">Text to draw</param>
        /// <param name="page">PDF page context</param>
        /// <param name="state">Graphics state</param>
        /// <param name="font">Font to use (PdfFontBase hierarchy)</param>
        /// <returns>Text advancement width for positioning</returns>
        float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font);
    }
}