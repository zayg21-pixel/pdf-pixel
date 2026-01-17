using SkiaSharp;
using System;

namespace PdfRender.Models;

/// <summary>
/// Rendering parameters for <see cref="PdfPage"/>.
/// </summary>
public class PdfRenderingParameters
{

    /// <summary>
    /// Simplified more with lower rendering quality.
    /// </summary>
    public bool PreviewMode { get; set; }

    /// <summary>
    /// Indicates whether the rendering is intended for printing.
    /// When true, affects annotation visibility (only annotations with Print flag are rendered).
    /// </summary>
    public bool PrintMode { get; set; }

    /// <summary>
    /// Actual device scale factor, if defined, all images will be downscaled
    /// to fit exact device scale, otherwise decoded in full size.
    /// </summary>
    public float? ScaleFactor { get; set; }

    /// <summary>
    /// Force image interpolation even if not downscaling.
    /// </summary>
    public bool ForceImageInterpolation { get; set; }

    /// <summary>
    /// Default number of samples for Postscript and Exponential functions when the point count is unknown.
    /// </summary>
    public int DefaultFunctionSamples { get; set; } = 64;

    /// <summary>
    /// Number of samples for Postscript and Exponential functions in preview mode when the point count is unknown.
    /// </summary>
    public int PreviewModeFunctionSamples { get; set; } = 8;

    /// <summary>
    /// Maximum number of tessellation vertices for mesh-based shadings.
    /// </summary>
    public int MaxTessellationVertices { get; set; } = 32;

    /// <summary>
    /// Maximum number of tessellation vertices for mesh-based shadings in preview mode.
    /// </summary>
    public int PreviewMaxTessellationVertices { get; set; } = 1;

    /// <summary>
    /// Returns a scaled size for the given original size based on the current
    /// </summary>
    /// <param name="size">Source size.</param>
    /// <param name="ctm">Current transformation matrix.</param>
    /// <returns>Null if size should not be changed, downscaled size otherwise.</returns>
    public SKSizeI? GetScaledSize(SKSizeI size, SKMatrix ctm)
    {
        if (!ScaleFactor.HasValue)
        {
            return default;
        }

        var unitMapped = ctm.MapPoint(new SKPoint(1, 1)) - ctm.MapPoint(new SKPoint(0, 0));

        float absParamScale = Math.Abs(ScaleFactor.Value);

        float unitPixelsX = Math.Abs(unitMapped.X) * absParamScale;
        float unitPixelsY = Math.Abs(unitMapped.Y) * absParamScale;

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        // only down-scaling is supported
        if (maxScale < 1f)
        {
            var newWidth = Math.Max(1, (int)Math.Floor(size.Width * maxScale));
            var newHeight = Math.Max(1, (int)Math.Floor(size.Height * maxScale));
            return new SKSizeI(newWidth, newHeight);
        }

        return default;
    }
}
