using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;

namespace PdfPixel.Commands;

/// <summary>
/// Immutable snapshot of graphics-state values needed for lazy shading evaluation.
/// Captured at record time so the <see cref="PdfDrawShadingCommand"/> can rebuild
/// shading geometry at Execute time without referencing <see cref="PdfGraphicsState"/>.
/// </summary>
public sealed class ShadingDecodingContext
{
    /// <summary>
    /// Initializes a new instance by capturing state from the current graphics state and shading.
    /// </summary>
    /// <param name="state">Current graphics state to capture values from.</param>
    /// <param name="shading">Shading model whose color space will be resolved.</param>
    public ShadingDecodingContext(PdfGraphicsState state, PdfShading shading)
    {
        Converter = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
        RenderingIntent = state.RenderingIntent;
        FullTransferFunction = state.FullTransferFunction;
    }

    /// <summary>
    /// Resolved color space converter for the shading's color space.
    /// </summary>
    public PdfColorSpaceConverter Converter { get; }

    /// <summary>
    /// Rendering intent captured from the graphics state.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Combined transfer function captured from the graphics state.
    /// </summary>
    public IColorTransform FullTransferFunction { get; }
}
