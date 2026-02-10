using SkiaSharp;
using PdfPixel.Shading.Model;
using PdfPixel.Rendering.State;

namespace PdfPixel.Rendering.Shading;

/// <summary>
/// Interface for shading drawing implementations (operator 'sh').
/// Implementations receive a parsed <see cref="PdfShading"/> model rather than the raw dictionary.
/// </summary>
public interface IShadingRenderer
{
    /// <summary>
    /// Draw a shading fill described by a parsed shading model.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state);
}
