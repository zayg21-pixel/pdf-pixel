using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;

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
    public ImageDecodingContext(PdfGraphicsState state)
    {
        FullTransferFunction = state.FullTransferFunction;
        IsType3Rendering = state.IsType3Rendering;
        FillColor = state.FillPaint.Color;
        FillAlpha = state.FillAlpha;
        BlendMode = PdfBlendModeNames.ToSkiaBlendMode(state.BlendMode);
    }

    /// <summary>
    /// Creates a context derived from <paramref name="source"/> but with explicit compositing overrides.
    /// Used for cases such as pattern-layer masking, where the desired blend mode and fill colour
    /// differ from the original graphics state.
    /// </summary>
    public ImageDecodingContext(ImageDecodingContext source, SKColor fillColor, float fillAlpha, SKBlendMode blendMode)
    {
        FullTransferFunction = source.FullTransferFunction;
        IsType3Rendering = source.IsType3Rendering;
        FillColor = fillColor;
        FillAlpha = fillAlpha;
        BlendMode = blendMode;
    }

    /// <summary>
    /// Current transformation matrix at the image position in the page coordinate space.
    /// </summary>
    public SKMatrix CTM { get; set; } = SKMatrix.Identity;

    /// <summary>
    /// Combined transfer function (internal + external) for color conversion.
    /// </summary>
    public IColorTransform FullTransferFunction { get; }

    /// <summary>
    /// Whether the rendering is in Type 3 glyph mode (affects downscale decisions).
    /// </summary>
    public bool IsType3Rendering { get; }

    /// <summary>
    /// Fill color from the graphics state, used for stencil mask rendering.
    /// </summary>
    public SKColor FillColor { get; }

    /// <summary>
    /// Image fill alpha.
    /// </summary>
    public float FillAlpha { get; }

    /// <summary>
    /// Skia blend mode for paint composition, converted from the PDF blend mode at construction.
    /// </summary>
    public SKBlendMode BlendMode { get; }

    /// <summary>
    /// Returns a scaled size for the given original size based on the current CTM.
    /// </summary>
    public SKSizeI? GetScaledSize(SKSizeI size)
    {
        var unitMapped = CTM.MapPoint(new SKPoint(1, 1)) - CTM.MapPoint(new SKPoint(0, 0));

        float unitPixelsX = Math.Abs(unitMapped.X);
        float unitPixelsY = Math.Abs(unitMapped.Y);

        float relScaleX = unitPixelsX / size.Width;
        float relScaleY = unitPixelsY / size.Height;

        float maxScale = Math.Max(relScaleX, relScaleY);

        if (maxScale < 1f)
        {
            var newWidth = Math.Max(1, (int)Math.Floor(size.Width * maxScale));
            var newHeight = Math.Max(1, (int)Math.Floor(size.Height * maxScale));
            return new SKSizeI(newWidth, newHeight);
        }

        return default;
    }
}
