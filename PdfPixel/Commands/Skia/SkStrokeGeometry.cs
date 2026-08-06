using PdfPixel.Commands.Converters;
using SkiaSharp;

namespace PdfPixel.Commands.Skia;

/// <summary>
/// Draws a stroke, and produces the geometry it covers, for a pen that <see cref="PdfStrokeScale"/> has
/// widened. A pen widened alike on both axes is round, and the paint already carries its whole width. One
/// widened differently is an ellipse, which no single Skia stroke width describes: stroking the path
/// shrunk by the widening factors and growing the resulting outline back by them leaves the geometry
/// where it was and gives the pen the shape the two axes ask for. Drawing a stroke and clipping to one
/// both come through here, so they agree on what the stroke covers.
/// </summary>
internal static class SkStrokeGeometry
{
    /// <summary>
    /// Draws <paramref name="path"/> stroked with <paramref name="paint"/> onto <paramref name="canvas"/>.
    /// </summary>
    public static void Draw(SKCanvas canvas, SKPath path, SKPaint paint, PdfCommandExecutionContext executionContext, in PdfStrokeScale strokeScale)
    {
        if (strokeScale.IsUniform)
        {
            canvas.DrawPath(path, paint);

            return;
        }

        using SKPath outline = CreateOutline(path, paint, executionContext, strokeScale);

        // The dash pattern and the stroke itself are already baked into the outline.
        paint.Style = SKPaintStyle.Fill;
        paint.PathEffect = null;
        canvas.DrawPath(outline, paint);
    }

    /// <summary>
    /// Returns the region <paramref name="path"/> covers when stroked with <paramref name="paint"/>, as a
    /// path in the current space, for callers that need the geometry rather than the draw.
    /// </summary>
    public static SKPath CreateOutline(SKPath path, SKPaint paint, PdfCommandExecutionContext executionContext, in PdfStrokeScale strokeScale)
    {
        // The matrix only tells Skia how finely to flatten curves; the outline comes back in the space it
        // was stroked in.
        SKMatrix flatteningMatrix = CommandHelpers.GetScaledMatrix(executionContext).ToSkMatrix();

        if (strokeScale.IsUniform)
        {
            using SKPathBuilder roundPenBuilder = new();

            return (paint.GetFillPath(path, roundPenBuilder, flatteningMatrix))
                ? roundPenBuilder.Detach()
                : new SKPath(path);
        }

        using SKPath strokeSpacePath = new(path);
        strokeSpacePath.Transform(SKMatrix.CreateScale(1f / strokeScale.X, 1f / strokeScale.Y));

        using SKPathBuilder outlineBuilder = new();
        if (!paint.GetFillPath(strokeSpacePath, outlineBuilder, flatteningMatrix))
        {
            return new SKPath(path);
        }

        SKPath outline = outlineBuilder.Detach();
        outline.Transform(SKMatrix.CreateScale(strokeScale.X, strokeScale.Y));

        return outline;
    }
}
