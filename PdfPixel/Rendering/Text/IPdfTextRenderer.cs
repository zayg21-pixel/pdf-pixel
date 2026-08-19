using PdfPixel.Commands.Model;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;

namespace PdfPixel.Rendering.Text;

/// <summary>
/// Defines the contract for drawing PDF text.
/// Implementations are responsible for rendering text using the provided font, graphics state, and page context.
/// </summary>
public interface IPdfTextRenderer
{
    /// <summary>
    /// Draws a text array with positioning adjustments using the given font and graphics state.
    /// Returns the total horizontal advancement (in user space units) after drawing the text.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="glyphs">Collection of pre-shaped glyphs to render.</param>
    /// <param name="state">The current graphics state.</param>
    /// <param name="font">The font to use for rendering.</param>
    /// <returns>The total advancement after drawing the text array.</returns>
    PdfSize DrawTextSequence(IPdfCommandProcessor processor, in ReadOnlyMemory<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font);
}
