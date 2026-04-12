using PdfPixel.Color.Transform;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Immutable snapshot of the graphics state values needed for image decoding.
/// Captured at command creation time so the command can later re-decode with
/// an updated <see cref="PdfRenderingParameters"/> at execution time.
/// </summary>
public sealed class ImageDecodingContext
{
    /// <summary>
    /// Creates a decoding context by capturing the relevant values from a <see cref="PdfGraphicsState"/>.
    /// </summary>
    /// <param name="state">The graphics state to snapshot.</param>
    public ImageDecodingContext(PdfGraphicsState state)
    {
        Ctm = state.CTM;
        FullTransferFunction = state.FullTransferFunction;
        IsType3Rendering = state.IsType3Rendering;
        FillColor = state.FillPaint.Color;
        FillAlpha = state.FillAlpha;
        BlendMode = state.BlendMode;
    }

    /// <summary>
    /// Current transformation matrix at the image position in the page coordinate space.
    /// Used together with <see cref="PdfRenderingParameters.ScaleFactor"/> to compute
    /// any downscale target size.
    /// </summary>
    public SKMatrix Ctm { get; }

    /// <summary>
    /// Combined transfer function (internal + external) for color conversion.
    /// </summary>
    public IColorTransform FullTransferFunction { get; }

    /// <summary>
    /// Whether the rendering is in Type 3 glyph mode (affects downscale decisions).
    /// </summary>
    public bool IsType3Rendering { get; }

    /// <summary>
    /// Fill color from the graphics state, used for image mask (stencil) rendering.
    /// </summary>
    public SKColor FillColor { get; }

    /// <summary>
    /// Fill alpha from the graphics state, used for paint opacity.
    /// </summary>
    public float FillAlpha { get; }

    /// <summary>
    /// Blend mode from the graphics state, used for paint composition.
    /// </summary>
    public PdfBlendMode BlendMode { get; }
}
