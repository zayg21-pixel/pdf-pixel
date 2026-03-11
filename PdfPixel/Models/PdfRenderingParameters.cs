using SkiaSharp;
using System;

namespace PdfPixel.Models;

/// <summary>
/// Rendering parameters for <see cref="PdfPage"/>.
/// </summary>
public class PdfRenderingParameters : IEquatable<PdfRenderingParameters>
{

    /// <summary>
    /// Simplified more with lower rendering quality.
    /// </summary>
    public bool PreviewMode { get; set; } // TODO: get rid and use granular properties.

    /// <summary>
    /// If true, clipping operations would be antialized, otherwise aliased.
    /// </summary>
    public bool AntialiasClip { get; set; } = false; // TODO: [HIGH] use in ALL clipping operations

    /// <summary>
    /// Indicates whether the rendering is intended for printing.
    /// When true, affects annotation visibility (only annotations with Print flag are rendered).
    /// </summary>
    public bool PrintMode { get; set; }

    /// <summary>
    /// If true - antialiazing will be enabled for rendering useful for CPU rendering to avoid jagged edges.
    /// </summary>
    public bool Antialias { get; set; }

    /// <summary>
    /// Actual device scale factor, if defined, all images will be downscaled
    /// to fit exact device scale, otherwise decoded in full size.
    /// </summary>
    public float? ScaleFactor { get; set; }

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
    /// Force image interpolation even if not downscaling.
    /// </summary>
    internal bool IsType3Rendering { get; set; }

    /// <summary>
    /// If true - antialiazing will be applied.
    /// </summary>
    internal bool ShouldAnialiaze => Antialias && !PreviewMode;

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

    /// <inheritdoc />
    public bool Equals(PdfRenderingParameters other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PreviewMode == other.PreviewMode &&
            PrintMode == other.PrintMode &&
            Antialias == other.Antialias &&
            ScaleFactor == other.ScaleFactor &&
            DefaultFunctionSamples == other.DefaultFunctionSamples &&
            PreviewModeFunctionSamples == other.PreviewModeFunctionSamples &&
            MaxTessellationVertices == other.MaxTessellationVertices &&
            PreviewMaxTessellationVertices == other.PreviewMaxTessellationVertices;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return Equals(obj as PdfRenderingParameters);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            PreviewMode,
            PrintMode,
            Antialias,
            ScaleFactor,
            DefaultFunctionSamples,
            PreviewModeFunctionSamples,
            MaxTessellationVertices,
            PreviewMaxTessellationVertices);
    }

    /// <summary>
    /// Determines whether two <see cref="PdfRenderingParameters"/> instances are equal.
    /// </summary>
    public static bool operator ==(PdfRenderingParameters left, PdfRenderingParameters right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="PdfRenderingParameters"/> instances are not equal.
    /// </summary>
    public static bool operator !=(PdfRenderingParameters left, PdfRenderingParameters right)
    {
        return !(left == right);
    }
}
