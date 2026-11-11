using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using PdfPixel.Snap.Transparency;
using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    internal SnapLayer Target => _state.Target;

    internal int ComponentCount => _state.Target.ColorSpace.Components;

    /// <summary>
    /// Gets the pixels drawing can reach: the current group within the current clip.
    /// </summary>
    private PdfIntegerRectangle GetDrawLimits()
    {
        return (_state.Clip != null)
            ? PdfIntegerRectangle.Intersect(_state.Target.Bounds, _state.Clip.Bounds)
            : _state.Target.Bounds;
    }

    internal Span<float> GetRowColors(int count) => Scratch.GetRowColors(count * ComponentCount);

    internal Span<float> GetRowObjectShapes(int count) => Scratch.GetRowObjectShapes(count);

    internal Span<float> GetRowAlphas(int count) => Scratch.GetRowAlphas(count);

    /// <summary>
    /// Composites a row of an elementary object into the current group with the soft mask in effect.
    /// </summary>
    internal void PaintRow(int y, int left, in ReadOnlySpan<float> coverage, in SnapRowValues colors, in SnapRowValues alphas, float constantAlpha, PdfPaint paint)
        => PaintRow(y, left, coverage, colors, alphas, constantAlpha, paint, GetMaskRow(y, left, coverage.Length));

    /// <summary>
    /// Composites a row of an elementary object with <paramref name="mask"/> in place of the soft mask in effect.
    /// </summary>
    internal void PaintRow(int y, int left, in ReadOnlySpan<float> coverage, in SnapRowValues colors, in SnapRowValues alphas, float constantAlpha, PdfPaint paint, in SnapRowValues mask)
    {
        SnapRowValues coverageValues = new(coverage, false);
        SnapSourceRow row = new(y, left, coverage.Length, colors, coverageValues, SnapRowValues.One, alphas, mask, constantAlpha, paint.AlphaIsShape);

        _state.Target.Compose(row, paint.BlendMode);
    }

    /// <summary>
    /// Returns the soft mask values over a run, 1 when no soft mask is in effect.
    /// </summary>
    internal SnapRowValues GetMaskRow(int y, int left, int count)
    {
        if (_state.SoftMask == null)
        {
            return SnapRowValues.One;
        }
        else
        {
            return GetSoftMaskRow(_state.SoftMask, y, left, count);
        }
    }

    /// <summary>
    /// Writes the paint's color in the target's color space and returns its alpha; under a tint the color comes from
    /// the tint.
    /// </summary>
    private float ResolveColor(PdfPaint paint, in Span<float> color)
    {
        if (_state.TintPaint != null)
        {
            return (ConvertPaintColor(_state.TintPaint, color) > 0) ? paint.Alpha : 0;
        }
        else
        {
            return ConvertPaintColor(paint, color);
        }
    }

    /// <summary>
    /// Writes the color of <paramref name="paint"/> in the target's color space and returns its alpha; a pattern color
    /// space without a pattern paints nothing (ISO 32000-2, Table 73).
    /// </summary>
    private float ConvertPaintColor(PdfPaint paint, in Span<float> color)
    {
        if (paint.Pattern == null && paint.ColorSpace is PdfPatternColorSpaceConverter)
        {
            color.Clear();

            return 0;
        }

        SnapColorConverter converter = _state.Target.GetConverter(paint);
        ReadOnlySpan<float> components = paint.Color.Components.Span;
        Span<float> source = stackalloc float[Math.Max(1, converter.SourceComponentCount)];

        source.Clear();
        components.Slice(0, Math.Min(components.Length, converter.SourceComponentCount)).CopyTo(source);
        converter.Convert(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(color));

        return paint.Alpha;
    }

    private void DrawShape<TPainter>(PdfPath outline, bool snapsRectangle, TPainter painter)
        where TPainter : ISnapRowPainter
    {
        PdfMatrix deviceMatrix = _state.DeviceMatrix;
        PdfIntegerRectangle? pixels = null;

        if (snapsRectangle)
        {
            pixels = PdfSnapGrid.GetShapePixels(outline, deviceMatrix);
        }

        if (pixels == null)
        {
            Scratch.Edges.Clear();
            Scratch.Edges.AddPath(outline, deviceMatrix);
        }

        Draw(pixels, outline.FillType, _executionContext.Parameters.Antialias, painter);
    }

    /// <summary>
    /// Draws the whole <paramref name="pixels"/>, or the edge list when <see langword="null"/>, within the draw limits
    /// and the clip.
    /// </summary>
    private void Draw<TPainter>(PdfIntegerRectangle? pixels, PdfPathFillType fillType, bool areaCoverage, TPainter painter)
        where TPainter : ISnapRowPainter
    {
        SnapShapeRowSink<TPainter> rowSink = new(painter, Scratch);
        PdfIntegerRectangle limits = GetDrawLimits();

        if (_state.Clip == null || _state.Clip.IsRectangle)
        {
            Rasterize(pixels, fillType, areaCoverage, limits, ref rowSink);
        }
        else
        {
            SnapClippedCoverageSink<SnapShapeRowSink<TPainter>> clippedSink = new(_state.Clip, Scratch.ClipBuffer, rowSink);
            Rasterize(pixels, fillType, areaCoverage, limits, ref clippedSink);
        }
    }

    private void Rasterize<TSink>(PdfIntegerRectangle? pixels, PdfPathFillType fillType, bool areaCoverage, in PdfIntegerRectangle limits, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        if (pixels != null)
        {
            Scratch.Rasterizer.FillRectangle(pixels.Value, limits, ref sink);
        }
        else
        {
            Scratch.Rasterizer.Rasterize(Scratch.Edges, fillType, areaCoverage, limits, ref sink);
        }
    }

    /// <summary>
    /// Returns whether <paramref name="userBounds"/>, widened past what a hairline pen may add, cannot reach the draw
    /// limits.
    /// </summary>
    private bool IsOutsideDrawLimits(in PdfRectangle userBounds)
    {
        PdfIntegerRectangle limits = GetDrawLimits();

        if (limits.Width <= 0 || limits.Height <= 0)
        {
            return true;
        }

        PdfRectangle deviceBounds = _state.DeviceMatrix.MapRect(userBounds);

        return deviceBounds.Right + DrawLimitsMargin < limits.Left
            || deviceBounds.Left - DrawLimitsMargin > limits.Right
            || deviceBounds.Bottom + DrawLimitsMargin < limits.Top
            || deviceBounds.Top - DrawLimitsMargin > limits.Bottom;
    }
}
