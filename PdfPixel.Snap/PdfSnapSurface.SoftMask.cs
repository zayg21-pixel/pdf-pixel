using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Transparency;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Sets the soft mask, drawn at once within the draw limits it was set in.
    /// </summary>
    private void SetSoftMask(PdfSoftMaskSource source)
    {
        if (IsEmpty)
        {
            return;
        }

        PdfIntegerRectangle bounds = (source.Group.Bounds != null) ? GetContentDeviceBounds(source.Group.Bounds.Value) : GetDrawLimits();

        _state.SetSoftMask(DrawSoftMask(source, bounds, GetOutsideMaskValue(source)));
    }

    /// <summary>
    /// Returns a soft mask's value outside its group's bounding box (ISO 32000-2 11.6.5.2): the backdrop's luminosity, or
    /// no alpha, through the transfer function.
    /// </summary>
    private static float GetOutsideMaskValue(PdfSoftMaskSource source)
    {
        PdfPaint? backdropPaint = source.BackdropPaint;

        if (backdropPaint != null)
        {
            return GetMaskValue(source, backdropPaint.ColorSpace, backdropPaint.Color.Components.Span, 1);
        }
        else
        {
            return GetMaskValue(source, PdfDeviceGrayColorSpaceConverter.Instance, ReadOnlySpan<float>.Empty, 0);
        }
    }

    private SnapRowValues GetSoftMaskRow(SnapSoftMask mask, int y, int left, int count)
    {
        Span<float> maskValues = mask.Values;
        PdfIntegerRectangle bounds = mask.Bounds;
        int start = Math.Max(left, bounds.Left);
        int end = Math.Min(left + count, bounds.Right);
        bool insideRow = y >= bounds.Top && y < bounds.Bottom;

        if (insideRow && start == left && end == left + count)
        {
            return new SnapRowValues(maskValues.Slice(((y - bounds.Top) * bounds.Width) + (left - bounds.Left), count), false);
        }
        else
        {
            Span<float> values = Scratch.GetRowMask(count);
            values.Fill(mask.OutsideValue);

            if (insideRow && end > start)
            {
                maskValues.Slice(((y - bounds.Top) * bounds.Width) + (start - bounds.Left), end - start).CopyTo(values.Slice(start - left));
            }

            return new SnapRowValues(values, false);
        }
    }

    /// <summary>
    /// Draws the mask group (ISO 32000-2 11.6.5.2) into a layer over <paramref name="bounds"/> and returns its values
    /// over the pixels it painted.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private SnapSoftMask DrawSoftMask(PdfSoftMaskSource source, in PdfIntegerRectangle bounds, float outsideValue)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new SnapSoftMask(PdfIntegerRectangle.Empty, outsideValue, Scratch);
        }

        PdfTransparencyGroupContext maskGroup = new(source.Group.Group.ColorSpace, isolated: true, knockout: false);
        SnapLayerGroup resolvedGroup = SnapLayerGroup.Resolve(maskGroup, parent: null);
        using SnapMemoryBlock block = Scratch.MemoryPool.Rent(SnapLayer.GetResultLength(bounds, resolvedGroup));
        using SnapLayer layer = new(SnapLayer.CreateResult(bounds, resolvedGroup, block.Array), bounds, resolvedGroup, parent: null, Scratch, GetConverters(), keepsShape: false);
        BeginLocalDrawing(layer, _state.DeviceMatrix, _state.VisibleRegion);

        try
        {
            DrawSoftMaskGroup(source);

            PdfIntegerRectangle painted = layer.PaintedBounds;
            SnapSoftMask mask = new(painted, outsideValue, Scratch);

            try
            {
                if (painted.Width > 0)
                {
                    WriteMaskValues(source, layer, painted, mask);
                }
            }
            catch
            {
                mask.Dispose();
                throw;
            }

            return mask;
        }
        finally
        {
            EndLocalDrawing();
        }
    }

    /// <summary>
    /// Writes the mask values of the <paramref name="painted"/> pixels of the mask group's <paramref name="layer"/> into
    /// <paramref name="mask"/>.
    /// </summary>
    private void WriteMaskValues(PdfSoftMaskSource source, SnapLayer layer, in PdfIntegerRectangle painted, SnapSoftMask mask)
    {
        PdfIntegerRectangle layerBounds = layer.Bounds;
        PdfIntegerRectangle resultPixels = new(painted.Left - layerBounds.Left, painted.Top - layerBounds.Top, painted.Right - layerBounds.Left, painted.Bottom - layerBounds.Top);
        SnapResultRows rows = new(layer.Result, resultPixels);
        IPdfImageRowProcessingStep[] steps = GetMaskValueSteps(source, layer.ColorSpace);
        using SnapMemoryBlock buffer = Scratch.MemoryPool.Rent(PdfImageRowProcessingPipeline.GetBufferLength(steps, rows.RowLength));
        PdfImageRowProcessingPipeline pipeline = new(rows, steps, mask, buffer.Array);

        pipeline.ProcessUntilRow(rows.RowCount - 1);
    }

    private static IPdfImageRowProcessingStep[] GetMaskValueSteps(PdfSoftMaskSource source, PdfColorSpaceConverter colorSpace)
    {
        IPdfImageRowProcessingStep valueStep;

        if (source.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            valueStep = new SnapLuminosityRowStep(colorSpace.GetBlendingColorModel());
        }
        else
        {
            valueStep = new SnapAlphaRowStep(colorSpace.Components);
        }

        if (source.TransferFunction != null)
        {
            return new IPdfImageRowProcessingStep[] { valueStep, new SnapTransferRowStep(source.TransferFunction.Functions[GrayTransferFunctionIndex]) };
        }
        else
        {
            return new IPdfImageRowProcessingStep[] { valueStep };
        }
    }

    /// <summary>
    /// Draws a soft mask's backdrop, when it has one, then its group clipped to its bounding box.
    /// </summary>
    private void DrawSoftMaskGroup(PdfSoftMaskSource source)
    {
        PdfIntegerRectangle rootBounds = _state.Target.Bounds;

        if (source.BackdropPaint != null)
        {
            float backdropAlpha = ResolveColor(source.BackdropPaint, _sourceColor);
            SnapSolidRowPainter backdropPainter = new(this, _sourceColor, backdropAlpha, source.BackdropPaint);
            Draw(rootBounds, PdfPathFillType.Winding, areaCoverage: false, backdropPainter);
        }

        PdfMatrix recordingMatrix = source.Group.Recording.Matrix;
        PdfPathBuilder builder = new();
        builder.AddRect(source.BoundingBox);

        Save();
        Concat(recordingMatrix);
        ApplyClip(builder.ToPath(), _state.DeviceMatrix);
        Concat(recordingMatrix.Invert());

        PdfIntegerRectangle bounds = GetDeviceBounds(recordingMatrix.MapRect(source.BoundingBox));
        DrawWithPaint(new SnapRectangleGeometry(this, bounds), bounds, DefaultGroupPaint, source.Group);

        Restore();
    }

    private static float GetMaskValue(PdfSoftMaskSource source, PdfColorSpaceConverter colorSpace, in ReadOnlySpan<float> color, float alpha)
    {
        float value = (source.Subtype == PdfSoftMaskSubtype.Luminosity)
            ? alpha * colorSpace.GetLuminosity(color)
            : alpha;

        if (source.TransferFunction != null)
        {
            ReadOnlySpan<float> transferred = source.TransferFunction.Functions[GrayTransferFunctionIndex].Evaluate(value);

            if (!transferred.IsEmpty)
            {
                value = transferred[0];
            }
        }

        return Math.Max(0, Math.Min(1, value));
    }
}
