using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Produces the region a stroked path covers, as a fill outline, for a pen that
/// <see cref="PdfStrokeScale"/> has widened. A pen widened alike on both axes is round, and its whole
/// width goes into the outline directly. One widened differently is an ellipse, which no single pen
/// width describes: stroking the path shrunk by the widening factors and growing the resulting outline
/// back by them leaves the geometry where it was and gives the pen the shape the two axes ask for.
/// Drawing a stroke and clipping to one both come through here, so they agree on what the stroke covers.
/// </summary>
internal static class PdfStrokeOutline
{
    /// <summary>
    /// Returns the region <paramref name="path"/> covers when stroked with <paramref name="style"/> under
    /// <paramref name="strokeScale"/>, in the space <paramref name="path"/> is given in. The width in
    /// <paramref name="style"/> is ignored: <paramref name="strokeScale"/> already carries the authored
    /// width along with the widening it needs.
    /// </summary>
    // TODO: [MEDIUM] an anisotropic pen rebuilds the whole path twice, once shrunk and once grown back.
    // Taking the widening into the outline build itself would emit the final geometry in a single pass,
    // and would let the device matrix be folded in at the same point.
    public static PdfPath Build(PdfPath path, PdfStrokeStyle style, in PdfStrokeScale strokeScale)
    {
        if (strokeScale.IsUniform)
        {
            return PdfStrokeOutlineBuilder.BuildOutline(path, style.WithLineWidth(strokeScale.UniformWidth));
        }

        PdfPath shrunkPath = path.Transform(PdfMatrix.CreateScale(1f / strokeScale.X, 1f / strokeScale.Y));
        PdfPath outline = PdfStrokeOutlineBuilder.BuildOutline(shrunkPath, GetShrunkStyle(style, strokeScale));

        return outline.Transform(PdfMatrix.CreateScale(strokeScale.X, strokeScale.Y));
    }

    // The pen travels a path shrunk by the widening factors, so the dash travels a shorter path and its
    // pattern shrinks with it.
    private static PdfStrokeStyle GetShrunkStyle(PdfStrokeStyle style, in PdfStrokeScale strokeScale)
    {
        PdfStrokeStyle shrunkStyle = style.WithLineWidth(strokeScale.PenWidth);

        if (style.DashPattern == null || style.DashPattern.Length == 0)
        {
            return shrunkStyle;
        }

        float dashScale = MathF.Max(strokeScale.X, strokeScale.Y);
        var intervals = new float[style.DashPattern.Length];

        for (int index = 0; index < intervals.Length; index++)
        {
            intervals[index] = style.DashPattern[index] / dashScale;
        }

        return shrunkStyle.WithDash(intervals, style.DashPhase / dashScale);
    }
}
