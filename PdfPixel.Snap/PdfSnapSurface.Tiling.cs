using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Pattern;
using PdfPixel.Snap.Rasterization;
using PdfPixel.Snap.Transparency;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Returns whether the pattern is better covered by repeating a rasterized period than by drawing the cell once per
    /// grid position. A step that does not advance, on the page or on the device, leaves no period to repeat, and a
    /// period wider than an image tile is not worth rasterizing.
    /// </summary>
    private bool CanTileByRepeating(PdfGroupSource source, PdfTiling tiling)
    {
        if (tiling.XStep <= 0 || tiling.YStep <= 0)
        {
            return false;
        }

        PdfSize deviceStep = GetDeviceStepSize(tiling, _state.DeviceMatrix.PreConcat(source.Recording.Matrix));
        int maximumStep = _executionContext.Parameters.ImageTileSize;

        return deviceStep.Width > 0
            && deviceStep.Height > 0
            && deviceStep.Width <= maximumStep
            && deviceStep.Height <= maximumStep;
    }

    /// <summary>
    /// Returns how far the pattern's steps advance on the device, measured along each of the pattern's own axes.
    /// </summary>
    private static PdfSize GetDeviceStepSize(PdfTiling tiling, in PdfMatrix patternToDevice)
    {
        PdfSize axisLengths = PdfSnapGrid.GetDeviceAxisLengths(patternToDevice);

        return new PdfSize(Math.Abs(tiling.XStep) * axisLengths.Width, Math.Abs(tiling.YStep) * axisLengths.Height);
    }

    /// <summary>
    /// Draws the pattern cell at every grid position reaching the draw limits (ISO 32000-2 8.7.3).
    /// </summary>
    private void DrawTiledRecording(PdfGroupSource source, PdfTiling tiling)
    {
        PdfIntegerRectangle limits = GetDrawLimits();
        int savesAfterOwnSave = BeginDrawRecording(source.Recording.TintPaint, source.Recording.Matrix);

        PdfRectangle deviceArea = new(limits.Left, limits.Top, limits.Right, limits.Bottom);
        PdfRectangle area = tiling.GetTilingArea(_state.DeviceMatrix.Invert().MapRect(deviceArea));

        DrawCells(source.Recording.Recorder, tiling, tiling.ComputeCellGrid(area));

        EndDrawRecording(savesAfterOwnSave);
    }

    /// <summary>
    /// Draws the cell at every position of <paramref name="grid"/>, each clipped to the cell's bounding box.
    /// </summary>
    private void DrawCells(PdfCommandRecorder recorder, PdfTiling tiling, in PdfIntegerRectangle grid)
    {
        PdfPathBuilder builder = new();
        builder.AddRect(tiling.BoundingBox);
        PdfPath boundingBox = builder.ToPath();

        for (int column = grid.Left; column <= grid.Right; column++)
        {
            for (int row = grid.Top; row <= grid.Bottom; row++)
            {
                Save();
                int savesAfterCellSave = _savedStates.Count;
                Concat(PdfMatrix.CreateTranslation(column * tiling.XStep, row * tiling.YStep));
                ApplyClip(boundingBox, _state.DeviceMatrix);
                ReplayCommands(recorder, savesAfterCellSave);
                RestoreTo(savesAfterCellSave);
                Restore();
            }
        }
    }

    /// <summary>
    /// Draws <paramref name="geometry"/> with the pattern by repeating its window, rasterized once over the painted
    /// area.
    /// </summary>
    private void DrawTilingPeriods<TGeometry>(TGeometry geometry, PdfGroupSource source, PdfTiling tiling, in PdfIntegerRectangle bounds, PdfPaint paint)
        where TGeometry : ISnapGeometry
    {
        PdfMatrix patternToDevice = _state.DeviceMatrix.PreConcat(source.Recording.Matrix);
        PdfMatrix deviceToPattern = patternToDevice.Invert();
        PdfRectangle paintedPattern = deviceToPattern.MapRect(new PdfRectangle(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        PdfSize deviceStep = GetDeviceStepSize(tiling, patternToDevice);
        SnapTilingAxis horizontal = SnapTilingAxis.Create(paintedPattern.Left, paintedPattern.Width, tiling.XStep, deviceStep.Width);
        SnapTilingAxis vertical = SnapTilingAxis.Create(paintedPattern.Top, paintedPattern.Height, tiling.YStep, deviceStep.Height);
        using SnapTilingWindow window = new(horizontal, vertical, ComponentCount, Scratch);

        RasterizeWindow(source, tiling, window);
        geometry.Draw(new SnapTilingPeriodPainter(this, window, deviceToPattern, paint));
    }

    /// <summary>
    /// Draws the pattern over the window's area into a layer in the target's color space and stores the result.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void RasterizeWindow(PdfGroupSource source, PdfTiling tiling, SnapTilingWindow window)
    {
        int width = window.Horizontal.Size;
        int height = window.Vertical.Size;
        PdfRectangle area = window.Area;

        // TODO: The window is drawn as an isolated group, which 11.6.7 NOTE 1 allows only when every object of the cell
        // is painted with blend mode Normal; any other cell is drawn in place, as the non-isolated group it is.
        PdfTransparencyGroupContext windowGroup = new(_state.Target.ColorSpace, isolated: true, knockout: false);

        PdfMatrix windowMatrix = PdfMatrix.CreateScale(window.Horizontal.Scale, window.Vertical.Scale).PreConcat(PdfMatrix.CreateTranslation(-area.Left, -area.Top));

        // TODO: A turned or skewed pattern can give a window bigger than this surface, as the window spans the
        // painted area's bounds in pattern space; a window layer is meant never to be bigger than the surface.
        PdfIntegerRectangle windowBounds = new(0, 0, width, height);
        SnapLayerGroup resolvedGroup = SnapLayerGroup.Resolve(windowGroup, parent: null);
        using SnapMemoryBlock block = Scratch.MemoryPool.Rent(SnapLayer.GetResultLength(windowBounds, resolvedGroup));
        using SnapLayer layer = new(SnapLayer.CreateResult(windowBounds, resolvedGroup, block.Array), windowBounds, resolvedGroup, parent: null, Scratch, GetConverters(), keepsShape: true);
        BeginLocalDrawing(layer, windowMatrix, visibleRegion: null);

        try
        {
            // A cell bigger than its step overlaps its neighbors, so every grid position reaching the window is drawn.
            _state.TintPaint = source.Recording.TintPaint;
            DrawCells(source.Recording.Recorder, tiling, tiling.ComputeCellGrid(area));

            int componentCount = layer.ColorSpace.Components;

            for (int row = 0; row < height; row++)
            {
                ReadOnlySpan<float> values = layer.ReadRow(row, 0, width);

                SnapGroupCompositing.SplitGroupResult(
                    values,
                    window.Colors.Slice(row * width * componentCount, width * componentCount),
                    window.Alphas.Slice(row * width, width),
                    componentCount);
                layer.ReadShapeRow(row, 0, width).CopyTo(window.Shapes.Slice(row * width, width));
            }
        }
        finally
        {
            EndLocalDrawing();
        }
    }
}
