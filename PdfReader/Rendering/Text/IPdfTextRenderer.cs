using PdfReader.Fonts.Model;
using PdfReader.Rendering.State;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering.Text;

/// <summary>
/// Defines the contract for drawing PDF text onto a SkiaSharp canvas.
/// Implementations are responsible for rendering text using the provided font, graphics state, and page context.
/// </summary>
public interface IPdfTextRenderer
{
    /// <summary>
    /// Draws a text array with positioning adjustments onto the specified canvas using the given font and graphics state.
    /// Returns the total horizontal advancement (in user space units) after drawing the text.
    /// </summary>
    /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
    /// <param name="glyphs">Collection of pre-shaped glyphs to render.</param>
    /// <param name="state">The current graphics state.</param>
    /// <param name="font">The font to use for rendering.</param>
    /// <returns>The total advancement after drawing the text array.</returns>
    SKSize DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font);
}