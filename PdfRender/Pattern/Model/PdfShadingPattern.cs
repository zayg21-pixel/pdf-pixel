using PdfRender.Color.Paint;
using PdfRender.Models;
using PdfRender.Rendering;
using PdfRender.Rendering.State;
using PdfRender.Shading;
using PdfRender.Shading.Model;
using SkiaSharp;

namespace PdfRender.Pattern.Model;

/// <summary>
/// Represents a shading pattern (/PatternType 2) in a PDF document.
/// Provides access to the referenced shading and optional extended graphics state.
/// Caches the base shader for performance.
/// </summary>
public sealed class PdfShadingPattern : PdfPattern
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingPattern"/> class with the specified parameters.
    /// </summary>
    /// <param name="page">The owning PDF page context.</param>
    /// <param name="sourceObject">The original source PDF object for the pattern.</param>
    /// <param name="shading">The shading object referenced by the pattern's /Shading entry.</param>
    /// <param name="matrix">The pattern transformation matrix.</param>
    /// <param name="extGState">Optional extended graphics state dictionary (may be null).</param>
    internal PdfShadingPattern(
        PdfObject sourceObject,
        PdfShading shading,
        SKMatrix matrix,
        PdfDictionary extGState)
        : base(sourceObject, matrix, PdfPatternType.Shading)
    {
        Shading = shading;
        ExtGState = extGState;
    }

    /// <summary>
    /// Gets the shading object referenced by the pattern's /Shading entry.
    /// </summary>
    public PdfShading Shading { get; }

    /// <summary>
    /// Gets the optional extended graphics state dictionary (may be null).
    /// </summary>
    public PdfDictionary ExtGState { get; } // TODO: [LOW] use

    internal override void RenderPattern(SKCanvas canvas, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        var matrix = SKMatrix.Concat(state.CTM.Invert(), PatternMatrix);
        var bounds = matrix.Invert().MapRect(renderTarget.ClipPath.Bounds);

        using var shadingPicture = PdfShadingBuilder.ToPicture(Shading, state, bounds);
        
        if (shadingPicture != null)
        {
            using var paint = PdfPaintFactory.CreateShadingPaint(state);
            canvas.Save();

            canvas.ClipPath(renderTarget.ClipPath, SKClipOperation.Intersect, antialias: !state.RenderingParameters.PreviewMode);

            canvas.Concat(matrix);

            if (Shading.BBox.HasValue)
            {
                canvas.ClipRect(Shading.BBox.Value, SKClipOperation.Intersect, antialias: !state.RenderingParameters.PreviewMode);
            }

            if (Shading.Background != null)
            {
                var colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(Shading.ColorSpaceConverter);
                var backgroundColor = colorSpace.ToSrgb(Shading.Background, state.RenderingIntent, state.FullTransferFunction);
                using var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor, state);
                canvas.DrawRect(canvas.LocalClipBounds, backgroundPaint);
            }

            canvas.DrawPicture(shadingPicture, paint);

            canvas.Restore();
        }
    }
}
