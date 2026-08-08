using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;

namespace PdfPixel.Commands;

/// <summary>
/// Immutable snapshot of graphics-state values needed for lazy shading evaluation.
/// Captured at record time so the <see cref="DrawShadingCommand"/> can rebuild
/// shading geometry at Execute time without referencing <see cref="PdfGraphicsState"/>.
/// </summary>
public sealed class ShadingDecodingContext
{
    /// <summary>
    /// Initializes a new instance by capturing state from the current graphics state and shading.
    /// </summary>
    /// <param name="state">Current graphics state to capture values from.</param>
    /// <param name="shading">Shading model whose color space will be resolved.</param>
    internal ShadingDecodingContext(PdfGraphicsState state, PdfShading shading)
    {
        Shading = shading;
        Converter = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceObject) ?? DeviceRgbConverter.Instance;
        FillAlpha = state.FillPaint.Alpha;
        RenderingIntent = state.RenderingIntent;
        TransferFunction = state.TransferFunction;
    }

    /// <summary>
    /// Shading model this context was captured for.
    /// </summary>
    public PdfShading Shading { get; }

    /// <summary>
    /// Resolved color space converter for the shading's color space.
    /// </summary>
    public PdfColorSpaceConverter Converter { get; }

    /// <summary>
    /// Shading fill alpha.
    /// </summary>
    public float FillAlpha { get; }

    /// <summary>
    /// Rendering intent captured from the graphics state.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Transfer function (TR) captured from the graphics state.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }
}
